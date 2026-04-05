using System;
using System.Collections;
using Mirror;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Gameplay.Configuration;
using Unity.BossRoom.Gameplay.GameState;
using Unity.BossRoom.Gameplay.GameplayObjects.Character.AI;
using Unity.BossRoom.Infrastructure;
using UnityEngine;
using UnityEngine.Serialization;
using Action = Unity.BossRoom.Gameplay.Actions.Action;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    /// <summary>
    /// Contains all SyncVars, Commands and server-side logic of a character.
    /// This class was separated in two to keep client and server context self contained.
    /// </summary>
    [RequireComponent(typeof(NetworkHealthState),
        typeof(NetworkLifeState),
        typeof(NetworkAvatarGuidState))]
    public class ServerCharacter : NetworkBehaviour, ITargetable
    {
        [FormerlySerializedAs("m_ClientVisualization")]
        [SerializeField]
        ClientCharacter m_ClientCharacter;

        public ClientCharacter clientCharacter => m_ClientCharacter;

        [SerializeField]
        CharacterClass m_CharacterClass;

        public CharacterClass CharacterClass
        {
            get
            {
                if (m_CharacterClass == null)
                {
                    m_CharacterClass = m_State.RegisteredAvatar.CharacterClass;
                }

                return m_CharacterClass;
            }

            set => m_CharacterClass = value;
        }

        // ---- SyncVars ----

        [SyncVar(hook = nameof(HandleMovementStatusChanged))]
        MovementStatus m_MovementStatus;

        /// <summary>Indicates how the character's movement should be depicted.</summary>
        public MovementStatus MovementStatus
        {
            get => m_MovementStatus;
            set
            {
                var old = m_MovementStatus;
                m_MovementStatus = value;
                if (isServer) HandleMovementStatusChanged(old, value);
            }
        }

        /// <summary>Fired on server and clients when MovementStatus changes.</summary>
        public event Action<MovementStatus, MovementStatus> MovementStatusChanged;

        void HandleMovementStatusChanged(MovementStatus oldValue, MovementStatus newValue)
        {
            MovementStatusChanged?.Invoke(oldValue, newValue);
        }

        [SyncVar(hook = nameof(HandleHeldNetworkObjectChanged))]
        uint m_HeldNetworkObject;

        public uint HeldNetworkObject
        {
            get => m_HeldNetworkObject;
            set
            {
                var old = m_HeldNetworkObject;
                m_HeldNetworkObject = value;
                if (isServer) HandleHeldNetworkObjectChanged(old, value);
            }
        }

        /// <summary>Fired on server and clients when HeldNetworkObject changes.</summary>
        public event System.Action<uint, uint> HeldNetworkObjectChanged;
        void HandleHeldNetworkObjectChanged(uint old, uint newId) => HeldNetworkObjectChanged?.Invoke(old, newId);

        /// <summary>
        /// Indicates whether this character is in "stealth mode" (invisible to monsters and other players).
        /// </summary>
        [SyncVar(hook = nameof(HandleIsStealthyChanged))]
        bool m_IsStealthy;

        public bool IsStealthy
        {
            get => m_IsStealthy;
            set
            {
                bool old = m_IsStealthy;
                m_IsStealthy = value;
                if (isServer) HandleIsStealthyChanged(old, value);
            }
        }

        /// <summary>Fired on server and clients when IsStealthy changes.</summary>
        public event Action<bool, bool> IsStealthyChanged;

        void HandleIsStealthyChanged(bool oldValue, bool newValue)
        {
            IsStealthyChanged?.Invoke(oldValue, newValue);
        }

        public NetworkHealthState NetHealthState { get; private set; }

        /// <summary>The active target of this character.</summary>
        [SyncVar(hook = nameof(HandleTargetIdChanged))]
        uint m_TargetId;

        public uint TargetId
        {
            get => m_TargetId;
            set
            {
                var old = m_TargetId;
                m_TargetId = value;
                if (isServer) HandleTargetIdChanged(old, value);
            }
        }

        /// <summary>Fired on server and clients when TargetId changes.</summary>
        public event System.Action<uint, uint> TargetIdChanged;
        void HandleTargetIdChanged(uint old, uint newId) => TargetIdChanged?.Invoke(old, newId);

        /// <summary>
        /// Current HP. This value is populated at startup time from CharacterClass data.
        /// </summary>
        public int HitPoints
        {
            get => NetHealthState.HitPoints;
            private set => NetHealthState.HitPoints = value;
        }

        public NetworkLifeState NetLifeState { get; private set; }

        /// <summary>
        /// Current LifeState. Only Players should enter the FAINTED state.
        /// </summary>
        public LifeState LifeState
        {
            get => NetLifeState.LifeState;
            private set => NetLifeState.LifeState = value;
        }

        /// <summary>Returns true if this Character is an NPC.</summary>
        public bool IsNpc => CharacterClass.IsNpc;

        public bool IsValidTarget => LifeState != LifeState.Dead;

        /// <summary>
        /// Returns true if the Character is currently in a state where it can play actions, false otherwise.
        /// </summary>
        public bool CanPerformActions => LifeState == LifeState.Alive;

        /// <summary>Character Type. This value is populated during character selection.</summary>
        public CharacterTypeEnum CharacterType => CharacterClass.CharacterType;

        private ServerActionPlayer m_ServerActionPlayer;

        /// <summary>
        /// The Character's ActionPlayer. This is mainly exposed for use by other Actions.
        /// </summary>
        public ServerActionPlayer ActionPlayer => m_ServerActionPlayer;

        [SerializeField]
        [Tooltip("If set to false, an NPC character will be denied its brain (won't attack or chase players)")]
        private bool m_BrainEnabled = true;

        [SerializeField]
        [Tooltip("Setting negative value disables destroying object after it is killed.")]
        private float m_KilledDestroyDelaySeconds = 3.0f;

        [SerializeField]
        [Tooltip("If set, the ServerCharacter will automatically play the StartingAction when it is created. ")]
        private Action m_StartingAction;

        [SerializeField]
        DamageReceiver m_DamageReceiver;

        [SerializeField]
        ServerCharacterMovement m_Movement;

        public ServerCharacterMovement Movement => m_Movement;

        [SerializeField]
        PhysicsWrapper m_PhysicsWrapper;

        public PhysicsWrapper physicsWrapper => m_PhysicsWrapper;

        [SerializeField]
        ServerAnimationHandler m_ServerAnimationHandler;

        public ServerAnimationHandler serverAnimationHandler => m_ServerAnimationHandler;

        private AIBrain m_AIBrain;
        NetworkAvatarGuidState m_State;

        /// <summary>
        /// The last character that dealt damage to us. Used for PvP kill attribution.
        /// </summary>
        ServerCharacter m_LastDamager;

        /// <summary>Last character that damaged this one. Null if no damage received yet.</summary>
        public ServerCharacter LastDamager => m_LastDamager;

        void Awake()
        {
            m_ServerActionPlayer = new ServerActionPlayer(this);
            NetLifeState = GetComponent<NetworkLifeState>();
            NetHealthState = GetComponent<NetworkHealthState>();
            m_State = GetComponent<NetworkAvatarGuidState>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            NetLifeState.LifeStateChanged += OnLifeStateChanged;
            m_DamageReceiver.DamageReceived += ReceiveHP;
            m_DamageReceiver.CollisionEntered += CollisionEntered;
            m_DamageReceiver.GetTotalDamageFunc += GetTotalDamage;

            if (IsNpc)
            {
                m_AIBrain = new AIBrain(this, m_ServerActionPlayer);
            }

            if (m_StartingAction != null)
            {
                var startingAction = new ActionRequestData() { ActionID = m_StartingAction.ActionID };
                PlayAction(ref startingAction);
            }
            InitializeHitPoints();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            NetLifeState.LifeStateChanged -= OnLifeStateChanged;

            if (m_DamageReceiver)
            {
                m_DamageReceiver.DamageReceived -= ReceiveHP;
                m_DamageReceiver.CollisionEntered -= CollisionEntered;
                m_DamageReceiver.GetTotalDamageFunc -= GetTotalDamage;
            }
        }

        /// <summary>
        /// Command to send movement input from a client to the server.
        /// </summary>
        [Command]
        public void CmdSendCharacterInput(Vector3 movementTarget)
        {
            if (LifeState == LifeState.Alive && !m_Movement.IsPerformingForcedMovement())
            {
                // if we're currently playing an interruptible action, interrupt it!
                if (m_ServerActionPlayer.GetActiveActionInfo(out ActionRequestData data))
                {
                    if (GameDataSource.Instance.GetActionPrototypeByID(data.ActionID).Config.ActionInterruptible)
                    {
                        m_ServerActionPlayer.ClearActions(false);
                    }
                }

                m_ServerActionPlayer.CancelRunningActionsByLogic(ActionLogic.Target, true); //clear target on move.
                m_Movement.SetMovementTarget(movementTarget);
            }
        }

        // ACTION SYSTEM

        /// <summary>
        /// Client->Server Command that sends a request to play an action.
        /// </summary>
        [Command]
        public void CmdPlayAction(ActionRequestData data)
        {
            ActionRequestData data1 = data;
            if (!GameDataSource.Instance.GetActionPrototypeByID(data1.ActionID).Config.IsFriendly)
            {
                // notify running actions that we're using a new attack. (e.g. so Stealth can cancel itself)
                ActionPlayer.OnGameplayActivity(Action.GameplayActivity.UsingAttackAction);
            }

            PlayAction(ref data1);
        }

        // UTILITY AND SPECIAL-PURPOSE Commands

        /// <summary>
        /// Called on server when the character's client decides they have stopped "charging up" an attack.
        /// </summary>
        [Command]
        public void CmdStopChargingUp()
        {
            m_ServerActionPlayer.OnGameplayActivity(Action.GameplayActivity.StoppedChargingUp);
        }

        void InitializeHitPoints()
        {
            HitPoints = CharacterClass.BaseHP.Value;

            if (!IsNpc)
            {
                ulong ownerClientId = connectionToClient != null ? (ulong)connectionToClient.connectionId : 0ul;
                SessionPlayerData? sessionPlayerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(ownerClientId);
                if (sessionPlayerData is { HasCharacterSpawned: true })
                {
                    // If the stored HP is valid (> 0), restore it.
                    // Otherwise keep full HP — this handles late-join / reconnect
                    // where the player was fainted before disconnecting.
                    if (sessionPlayerData.Value.CurrentHitPoints > 0)
                    {
                        HitPoints = sessionPlayerData.Value.CurrentHitPoints;
                    }
                    // else: keep BaseHP (already set above) — spawn alive with full health
                }
            }
        }

        /// <summary>
        /// Play a sequence of actions!
        /// </summary>
        public void PlayAction(ref ActionRequestData action)
        {
            //the character needs to be alive in order to be able to play actions
            if (LifeState == LifeState.Alive && !m_Movement.IsPerformingForcedMovement())
            {
                if (action.CancelMovement)
                {
                    m_Movement.CancelMove();
                }

                m_ServerActionPlayer.PlayAction(ref action);
            }
        }

        void OnLifeStateChanged(LifeState prevLifeState, LifeState lifeState)
        {
            if (lifeState != LifeState.Alive)
            {
                m_ServerActionPlayer.ClearActions(true);
                m_Movement.CancelMove();
            }
        }

        IEnumerator KilledDestroyProcess()
        {
            yield return new WaitForSeconds(m_KilledDestroyDelaySeconds);

            if (netIdentity != null)
            {
                NetworkServer.Destroy(gameObject);
            }
        }

        /// <summary>
        /// Receive an HP change from somewhere. Could be healing or damage.
        /// </summary>
        /// <param name="inflicter">Person dishing out this damage/healing. Can be null.</param>
        /// <param name="HP">The HP to receive. Positive value is healing. Negative is damage.</param>
        void ReceiveHP(ServerCharacter inflicter, int HP)
        {
            if (HP > 0)
            {
                m_ServerActionPlayer.OnGameplayActivity(Action.GameplayActivity.Healed);
                float healingMod = m_ServerActionPlayer.GetBuffedValue(Action.BuffableValue.PercentHealingReceived);
                HP = (int)(HP * healingMod);
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Don't apply damage if god mode is on
                if (NetLifeState.IsGodMode)
                {
                    return;
                }
#endif

                m_ServerActionPlayer.OnGameplayActivity(Action.GameplayActivity.AttackedByEnemy);
                float damageMod = m_ServerActionPlayer.GetBuffedValue(Action.BuffableValue.PercentDamageReceived);
                HP = (int)(HP * damageMod);

                serverAnimationHandler.NetworkAnimator.SetTrigger("HitReact1");

                // Track last damager for PvP kill attribution
                if (inflicter != null)
                {
                    m_LastDamager = inflicter;
                }
            }

            HitPoints = Mathf.Clamp(HitPoints + HP, 0, CharacterClass.BaseHP.Value);

            if (m_AIBrain != null)
            {
                //let the brain know about the modified amount of damage we received.
                m_AIBrain.ReceiveHP(inflicter, HP);
            }

            //we can't currently heal a dead character back to Alive state.
            //that's handled by a separate function.
            if (HitPoints <= 0)
            {
                if (IsNpc)
                {
                    if (m_KilledDestroyDelaySeconds >= 0.0f && LifeState != LifeState.Dead)
                    {
                        StartCoroutine(KilledDestroyProcess());
                    }

                    LifeState = LifeState.Dead;
                }
                else
                {
                    LifeState = LifeState.Fainted;
                }

                // Notify PvP score manager about the kill
                NotifyPvPKill();

                m_ServerActionPlayer.ClearActions(false);
            }
        }

        /// <summary>
        /// Notifies PvPScoreManager about a kill event when this character dies.
        /// </summary>
        void NotifyPvPKill()
        {
            if (PvPScoreManager.Instance == null) return;

            if (IsNpc)
            {
                // An NPC died — if a player killed it, award +1 point
                if (m_LastDamager != null && !m_LastDamager.IsNpc)
                {
                    PvPScoreManager.Instance.OnPlayerKilledNpc(m_LastDamager.netId);
                }
            }
            else
            {
                // A player died
                if (m_LastDamager != null && !m_LastDamager.IsNpc)
                {
                    // Killed by another player → killer gets +3
                    PvPScoreManager.Instance.OnPlayerKilledPlayer(m_LastDamager.netId);
                }
                else
                {
                    // Killed by NPC (or unknown) → victim loses 3
                    PvPScoreManager.Instance.OnPlayerKilledByNpc(netId);
                }
            }

            m_LastDamager = null;
        }

        /// <summary>
        /// Determines a gameplay variable for this character.
        /// </summary>
        public float GetBuffedValue(Action.BuffableValue buffType)
        {
            return m_ServerActionPlayer.GetBuffedValue(buffType);
        }

        /// <summary>
        /// Receive a Life State change that brings Fainted characters back to Alive state.
        /// </summary>
        public void Revive(ServerCharacter inflicter, int HP)
        {
            if (LifeState == LifeState.Fainted)
            {
                HitPoints = Mathf.Clamp(HP, 0, CharacterClass.BaseHP.Value);
                NetLifeState.LifeState = LifeState.Alive;
            }
        }

        void Update()
        {
            m_ServerActionPlayer.OnUpdate();
            if (m_AIBrain != null && LifeState == LifeState.Alive && m_BrainEnabled)
            {
                m_AIBrain.Update();
            }
        }

        void CollisionEntered(Collision collision)
        {
            if (m_ServerActionPlayer != null)
            {
                m_ServerActionPlayer.CollisionEntered(collision);
            }
        }

        int GetTotalDamage()
        {
            return Math.Max(0, CharacterClass.BaseHP.Value - HitPoints);
        }

        /// <summary>This character's AIBrain. Will be null if this is not an NPC.</summary>
        public AIBrain AIBrain { get { return m_AIBrain; } }

        // ---- Client lifecycle forwarding to ClientCharacter (child graphics object) ----

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (m_ClientCharacter)
                m_ClientCharacter.OnNetworkStartClient(this);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (m_ClientCharacter)
                m_ClientCharacter.OnNetworkStopClient();
        }

        // ---- ClientRpc forwarders for ClientCharacter ----
        // ClientCharacter is a MonoBehaviour on a child graphics object and cannot have its own
        // NetworkIdentity in Mirror. These RPCs live here on the root and forward to it.

        [ClientRpc]
        public void ClientPlayActionRpc(ActionRequestData data)
        {
            if (m_ClientCharacter)
                m_ClientCharacter.PlayAction(data);
        }

        [ClientRpc]
        public void ClientCancelAllActionsRpc()
        {
            if (m_ClientCharacter)
                m_ClientCharacter.CancelAllActions();
        }

        [ClientRpc]
        public void ClientCancelActionsByPrototypeIDRpc(ActionID actionPrototypeID)
        {
            if (m_ClientCharacter)
                m_ClientCharacter.CancelActionsByPrototypeID(actionPrototypeID);
        }

        [ClientRpc]
        public void ClientStopChargingUpRpc(float percentCharged)
        {
            if (m_ClientCharacter)
                m_ClientCharacter.StopChargingUp(percentCharged);
        }
    }
}
