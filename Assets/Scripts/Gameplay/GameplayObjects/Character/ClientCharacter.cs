using System;
using Mirror;
using Unity.BossRoom.CameraUtils;
using Unity.BossRoom.Gameplay.UserInput;
using Unity.BossRoom.Gameplay.Configuration;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Utils;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    /// <summary>
    /// <see cref="ClientCharacter"/> is responsible for displaying a character on the client's screen
    /// based on state information sent by the server.
    /// </summary>
    /// <remarks>
    /// This is a MonoBehaviour (not NetworkBehaviour) because it lives on a child graphics object.
    /// Mirror requires NetworkIdentity on the same GameObject as any NetworkBehaviour, and the
    /// NetworkIdentity is on the parent. RPCs are routed through ServerCharacter on the root.
    /// </remarks>
    public class ClientCharacter : MonoBehaviour
    {
        [SerializeField]
        Animator m_ClientVisualsAnimator;

        [SerializeField]
        VisualizationConfiguration m_VisualizationConfiguration;

        /// <summary>Returns a reference to the active Animator for this visualization.</summary>
        public Animator OurAnimator => m_ClientVisualsAnimator;

        /// <summary>Returns the targeting-reticule prefab for this character visualization.</summary>
        public GameObject TargetReticulePrefab => m_VisualizationConfiguration.TargetReticule;

        /// <summary>Returns the Material to plug into the reticule when the selected entity is hostile.</summary>
        public Material ReticuleHostileMat => m_VisualizationConfiguration.ReticuleHostileMat;

        /// <summary>Returns the Material to plug into the reticule when the selected entity is friendly.</summary>
        public Material ReticuleFriendlyMat => m_VisualizationConfiguration.ReticuleFriendlyMat;

        CharacterSwap m_CharacterSwapper;

        public CharacterSwap CharacterSwap => m_CharacterSwapper;

        public bool CanPerformActions => m_ServerCharacter.CanPerformActions;

        ServerCharacter m_ServerCharacter;

        public ServerCharacter serverCharacter => m_ServerCharacter;

        ClientActionPlayer m_ClientActionViz;

        PositionLerper m_PositionLerper;

        RotationLerper m_RotationLerper;

        // this value suffices for both positional and rotational interpolations
        const float k_LerpTime = 0.08f;

        Vector3 m_LerpedPosition;

        Quaternion m_LerpedRotation;

        float m_CurrentSpeed;

        /// <summary>
        /// Called on all clients to play an action's visual effects.
        /// Invoked by ServerCharacter's ClientRpc forwarder.
        /// </summary>
        public void PlayAction(ActionRequestData data)
        {
            m_ClientActionViz.PlayAction(ref data);
        }

        /// <summary>
        /// Called on all clients to cancel all active action FXs.
        /// </summary>
        public void CancelAllActions()
        {
            m_ClientActionViz.CancelAllActions();
        }

        /// <summary>
        /// Called on all clients to cancel action FXs of a certain type.
        /// </summary>
        public void CancelActionsByPrototypeID(ActionID actionPrototypeID)
        {
            m_ClientActionViz.CancelAllActionsWithSamePrototypeID(actionPrototypeID);
        }

        /// <summary>
        /// Called on all clients when this character has stopped "charging up" an attack.
        /// </summary>
        public void StopChargingUp(float percentCharged)
        {
            m_ClientActionViz.OnStoppedChargingUp(percentCharged);
        }

        void Awake()
        {
            enabled = false;
        }

        /// <summary>
        /// Called by ServerCharacter when the network object starts on the client.
        /// Replaces the former NetworkBehaviour.OnStartClient override.
        /// </summary>
        public void OnNetworkStartClient(ServerCharacter parentServerCharacter)
        {
            if (transform.parent == null)
            {
                return;
            }

            enabled = true;

            m_ClientActionViz = new ClientActionPlayer(this);

            m_ServerCharacter = parentServerCharacter;

            m_ServerCharacter.IsStealthyChanged += OnStealthyChanged;
            m_ServerCharacter.MovementStatusChanged += OnMovementStatusChanged;
            OnMovementStatusChanged(MovementStatus.Normal, m_ServerCharacter.MovementStatus);

            // sync our visualization position & rotation to the most up to date version received from server
            transform.SetPositionAndRotation(serverCharacter.physicsWrapper.Transform.position,
                serverCharacter.physicsWrapper.Transform.rotation);
            m_LerpedPosition = transform.position;
            m_LerpedRotation = transform.rotation;

            // similarly, initialize start position and rotation for smooth lerping purposes
            m_PositionLerper = new PositionLerper(serverCharacter.physicsWrapper.Transform.position, k_LerpTime);
            m_RotationLerper = new RotationLerper(serverCharacter.physicsWrapper.Transform.rotation, k_LerpTime);

            if (!m_ServerCharacter.IsNpc)
            {
                name = "AvatarGraphics" + (m_ServerCharacter.connectionToClient != null
                    ? (object)m_ServerCharacter.connectionToClient.connectionId
                    : m_ServerCharacter.netId);

                if (m_ServerCharacter.TryGetComponent(out ClientPlayerAvatarNetworkAnimator characterNetworkAnimator))
                {
                    m_ClientVisualsAnimator = characterNetworkAnimator.Animator;
                }

                m_CharacterSwapper = GetComponentInChildren<CharacterSwap>();

                // ...and visualize the current char-select value that we know about
                SetAppearanceSwap();

                if (m_ServerCharacter.isOwned)
                {
                    ActionRequestData data = new ActionRequestData { ActionID = GameDataSource.Instance.GeneralTargetActionPrototype.ActionID };
                    m_ClientActionViz.PlayAction(ref data);
                    gameObject.AddComponent<CameraController>();

                    if (m_ServerCharacter.TryGetComponent(out ClientInputSender inputSender))
                    {
                        // anticipated actions will only be played on non-host, owning clients
                        if (!NetworkServer.active)
                        {
                            inputSender.ActionInputEvent += OnActionInput;
                        }
                        inputSender.ClientMoveEvent += OnMoveInput;
                    }
                }
            }
        }

        /// <summary>
        /// Called by ServerCharacter when the network object stops on the client.
        /// Replaces the former NetworkBehaviour.OnStopClient override.
        /// </summary>
        public void OnNetworkStopClient()
        {
            if (m_ServerCharacter)
            {
                m_ServerCharacter.IsStealthyChanged -= OnStealthyChanged;

                if (m_ServerCharacter.TryGetComponent(out ClientInputSender sender))
                {
                    sender.ActionInputEvent -= OnActionInput;
                    sender.ClientMoveEvent -= OnMoveInput;
                }
            }

            enabled = false;
        }

        void OnActionInput(ActionRequestData data)
        {
            m_ClientActionViz.AnticipateAction(ref data);
        }

        void OnMoveInput(Vector3 position)
        {
            if (!IsAnimating())
            {
                OurAnimator.SetTrigger(m_VisualizationConfiguration.AnticipateMoveTriggerID);
            }
        }

        void OnStealthyChanged(bool oldValue, bool newValue)
        {
            SetAppearanceSwap();
        }

        void SetAppearanceSwap()
        {
            if (m_CharacterSwapper)
            {
                var specialMaterialMode = CharacterSwap.SpecialMaterialMode.None;
                if (m_ServerCharacter.IsStealthy)
                {
                    if (m_ServerCharacter.isOwned)
                    {
                        specialMaterialMode = CharacterSwap.SpecialMaterialMode.StealthySelf;
                    }
                    else
                    {
                        specialMaterialMode = CharacterSwap.SpecialMaterialMode.StealthyOther;
                    }
                }

                m_CharacterSwapper.SwapToModel(specialMaterialMode);
            }
        }

        /// <summary>
        /// Returns the value we should set the Animator's "Speed" variable, given current gameplay conditions.
        /// </summary>
        float GetVisualMovementSpeed(MovementStatus movementStatus)
        {
            if (m_ServerCharacter.NetLifeState.LifeState != LifeState.Alive)
            {
                return m_VisualizationConfiguration.SpeedDead;
            }

            switch (movementStatus)
            {
                case MovementStatus.Idle:
                    return m_VisualizationConfiguration.SpeedIdle;
                case MovementStatus.Normal:
                    return m_VisualizationConfiguration.SpeedNormal;
                case MovementStatus.Uncontrolled:
                    return m_VisualizationConfiguration.SpeedUncontrolled;
                case MovementStatus.Slowed:
                    return m_VisualizationConfiguration.SpeedSlowed;
                case MovementStatus.Hasted:
                    return m_VisualizationConfiguration.SpeedHasted;
                case MovementStatus.Walking:
                    return m_VisualizationConfiguration.SpeedWalking;
                default:
                    throw new Exception($"Unknown MovementStatus {movementStatus}");
            }
        }

        void OnMovementStatusChanged(MovementStatus previousValue, MovementStatus newValue)
        {
            m_CurrentSpeed = GetVisualMovementSpeed(newValue);
        }

        void Update()
        {
            // On the host, Characters are translated via ServerCharacterMovement's FixedUpdate method.
            // Smoothing here eliminates camera jitter on the host.
            if (NetworkServer.active && NetworkClient.active) // host
            {
                m_LerpedPosition = m_PositionLerper.LerpPosition(m_LerpedPosition,
                    serverCharacter.physicsWrapper.Transform.position);
                m_LerpedRotation = m_RotationLerper.LerpRotation(m_LerpedRotation,
                    serverCharacter.physicsWrapper.Transform.rotation);
                transform.SetPositionAndRotation(m_LerpedPosition, m_LerpedRotation);
            }

            if (m_ClientVisualsAnimator)
            {
                // set Animator variables here
                OurAnimator.SetFloat(m_VisualizationConfiguration.SpeedVariableID, m_CurrentSpeed);
            }

            m_ClientActionViz.OnUpdate();
        }

        void OnAnimEvent(string id)
        {
            m_ClientActionViz.OnAnimEvent(id);
        }

        public bool IsAnimating()
        {
            if (OurAnimator.GetFloat(m_VisualizationConfiguration.SpeedVariableID) > 0.0) { return true; }

            for (int i = 0; i < OurAnimator.layerCount; i++)
            {
                if (OurAnimator.GetCurrentAnimatorStateInfo(i).tagHash != m_VisualizationConfiguration.BaseNodeTagID)
                {
                    //we are in an active node, not the default "nothing" node.
                    return true;
                }
            }

            return false;
        }
    }
}
