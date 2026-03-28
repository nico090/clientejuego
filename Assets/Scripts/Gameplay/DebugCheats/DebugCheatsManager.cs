using Mirror;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Gameplay.Messages;
using Unity.BossRoom.Infrastructure;
using Unity.BossRoom.Infrastructure;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Unity.BossRoom.DebugCheats
{
    /// <summary>
    /// Handles debug cheat events, applies them on the server and logs them on all clients. This class is only
    /// available in the editor or for development builds.
    /// </summary>
    public class DebugCheatsManager : NetworkBehaviour
    {
        [SerializeField]
        GameObject m_DebugCheatsPanel;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField]
        [Tooltip("Enemy to spawn. Make sure this is registered in the NetworkManager's spawnable prefabs!")]
        GameObject m_EnemyPrefab;

        [SerializeField]
        [Tooltip("Boss to spawn. Make sure this is registered in the NetworkManager's spawnable prefabs!")]
        GameObject m_BossPrefab;

        [SerializeField]
        InputActionReference m_ToggleCheatsAction;

        SwitchedDoor m_SwitchedDoor;

        SwitchedDoor SwitchedDoor
        {
            get
            {
                if (m_SwitchedDoor == null)
                {
                    m_SwitchedDoor = FindAnyObjectByType<SwitchedDoor>();
                }

                return m_SwitchedDoor;
            }
        }

        bool m_DestroyPortalsOnNextToggle = true;

        [Inject]
        IPublisher<CheatUsedMessage> m_CheatUsedMessagePublisher;

        void Start()
        {
            m_ToggleCheatsAction.action.performed += OnToggleCheatsActionPerformed;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            m_ToggleCheatsAction.action.performed -= OnToggleCheatsActionPerformed;
        }

        void OnToggleCheatsActionPerformed(InputAction.CallbackContext obj)
        {
            m_DebugCheatsPanel.SetActive(!m_DebugCheatsPanel.activeSelf);
        }

        public void SpawnEnemy()
        {
            CmdSpawnEnemy();
        }

        public void SpawnBoss()
        {
            CmdSpawnBoss();
        }

        public void KillTarget()
        {
            CmdKillTarget();
        }

        public void KillAllEnemies()
        {
            CmdKillAllEnemies();
        }

        public void ToggleGodMode()
        {
            CmdToggleGodMode();
        }

        public void HealPlayer()
        {
            CmdHealPlayer();
        }

        public void ToggleSuperSpeed()
        {
            CmdToggleSuperSpeed();
        }

        public void ToggleTeleportMode()
        {
            CmdToggleTeleportMode();
        }

        public void ToggleDoor()
        {
            CmdToggleDoor();
        }

        public void TogglePortals()
        {
            CmdTogglePortals();
        }

        public void GoToPostGame()
        {
            CmdGoToPostGame();
        }

        [Command(requiresAuthority = false)]
        void CmdSpawnEnemy(NetworkConnectionToClient sender = null)
        {
            var newEnemy = Instantiate(m_EnemyPrefab);
            NetworkServer.Spawn(newEnemy);
            PublishCheatUsedMessage(sender, "SpawnEnemy");
        }

        [Command(requiresAuthority = false)]
        void CmdSpawnBoss(NetworkConnectionToClient sender = null)
        {
            var newBoss = Instantiate(m_BossPrefab);
            NetworkServer.Spawn(newBoss);
            PublishCheatUsedMessage(sender, "SpawnBoss");
        }

        [Command(requiresAuthority = false)]
        void CmdKillTarget(NetworkConnectionToClient sender = null)
        {
            var clientId = sender != null ? (ulong)sender.connectionId : 0ul;
            var playerServerCharacter = PlayerServerCharacter.GetPlayerServerCharacter(clientId);
            if (playerServerCharacter != null)
            {
                var targetId = playerServerCharacter.TargetId;
                if (NetworkServer.spawned.TryGetValue(targetId, out NetworkIdentity ni))
                {
                    var damageable = ni.GetComponent<IDamageable>();
                    if (damageable != null && damageable.IsDamageable())
                    {
                        damageable.ReceiveHitPoints(playerServerCharacter, int.MinValue);
                        PublishCheatUsedMessage(sender, "KillTarget");
                    }
                    else
                    {
                        Debug.Log($"Target {targetId} has no IDamageable component or cannot be damaged.");
                    }
                }
            }
        }

        [Command(requiresAuthority = false)]
        void CmdKillAllEnemies(NetworkConnectionToClient sender = null)
        {
            foreach (var serverCharacter in FindObjectsByType<ServerCharacter>(FindObjectsSortMode.None))
            {
                if (serverCharacter.IsNpc && serverCharacter.LifeState == LifeState.Alive)
                {
                    if (serverCharacter.gameObject.TryGetComponent(out IDamageable damageable))
                    {
                        damageable.ReceiveHitPoints(null, -serverCharacter.HitPoints);
                    }
                }
            }

            PublishCheatUsedMessage(sender, "KillAllEnemies");
        }

        [Command(requiresAuthority = false)]
        void CmdToggleGodMode(NetworkConnectionToClient sender = null)
        {
            var clientId = sender != null ? (ulong)sender.connectionId : 0ul;
            var playerServerCharacter = PlayerServerCharacter.GetPlayerServerCharacter(clientId);
            if (playerServerCharacter != null)
            {
                playerServerCharacter.NetLifeState.IsGodMode = !playerServerCharacter.NetLifeState.IsGodMode;
                PublishCheatUsedMessage(sender, "ToggleGodMode");
            }
        }

        [Command(requiresAuthority = false)]
        void CmdHealPlayer(NetworkConnectionToClient sender = null)
        {
            var clientId = sender != null ? (ulong)sender.connectionId : 0ul;
            var playerServerCharacter = PlayerServerCharacter.GetPlayerServerCharacter(clientId);
            if (playerServerCharacter != null)
            {
                var baseHp = playerServerCharacter.CharacterClass.BaseHP.Value;
                if (playerServerCharacter.LifeState == LifeState.Fainted)
                {
                    playerServerCharacter.Revive(null, baseHp);
                }
                else
                {
                    if (playerServerCharacter.gameObject.TryGetComponent(out IDamageable damageable))
                    {
                        damageable.ReceiveHitPoints(null, baseHp);
                    }
                }

                PublishCheatUsedMessage(sender, "HealPlayer");
            }
        }

        [Command(requiresAuthority = false)]
        void CmdToggleSuperSpeed(NetworkConnectionToClient sender = null)
        {
            var clientId = sender != null ? (ulong)sender.connectionId : 0ul;
            foreach (var playerServerCharacter in PlayerServerCharacter.GetPlayerServerCharacters())
            {
                if (playerServerCharacter.connectionToClient != null &&
                    (ulong)playerServerCharacter.connectionToClient.connectionId == clientId)
                {
                    playerServerCharacter.Movement.SpeedCheatActivated = !playerServerCharacter.Movement.SpeedCheatActivated;
                    break;
                }
            }

            PublishCheatUsedMessage(sender, "ToggleSuperSpeed");
        }

        [Command(requiresAuthority = false)]
        void CmdToggleTeleportMode(NetworkConnectionToClient sender = null)
        {
            var clientId = sender != null ? (ulong)sender.connectionId : 0ul;
            foreach (var playerServerCharacter in PlayerServerCharacter.GetPlayerServerCharacters())
            {
                if (playerServerCharacter.connectionToClient != null &&
                    (ulong)playerServerCharacter.connectionToClient.connectionId == clientId)
                {
                    playerServerCharacter.Movement.TeleportModeActivated = !playerServerCharacter.Movement.TeleportModeActivated;
                    break;
                }
            }

            PublishCheatUsedMessage(sender, "ToggleTeleportMode");
        }

        [Command(requiresAuthority = false)]
        void CmdToggleDoor(NetworkConnectionToClient sender = null)
        {
            if (SwitchedDoor != null)
            {
                SwitchedDoor.ForceOpen = !SwitchedDoor.ForceOpen;
                PublishCheatUsedMessage(sender, "ToggleDoor");
            }
            else
            {
                Debug.Log("Could not activate ToggleDoor cheat. Door not found.");
            }
        }

        [Command(requiresAuthority = false)]
        void CmdTogglePortals(NetworkConnectionToClient sender = null)
        {
            foreach (var portal in FindObjectsByType<EnemyPortal>(FindObjectsSortMode.None))
            {
                if (m_DestroyPortalsOnNextToggle)
                {
                    portal.ForceDestroy();
                }
                else
                {
                    portal.ForceRestart();
                }
            }

            m_DestroyPortalsOnNextToggle = !m_DestroyPortalsOnNextToggle;
            PublishCheatUsedMessage(sender, "TogglePortals");
        }

        [Command(requiresAuthority = false)]
        void CmdGoToPostGame(NetworkConnectionToClient sender = null)
        {
            SceneLoaderWrapper.Instance.LoadScene("PostGame", useNetworkSceneManager: true);
            PublishCheatUsedMessage(sender, "GoToPostGame");
        }

        void PublishCheatUsedMessage(NetworkConnectionToClient sender, string cheatUsed)
        {
            var clientId = sender != null ? (ulong)sender.connectionId : 0ul;
            var playerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(clientId);
            if (playerData.HasValue)
            {
                m_CheatUsedMessagePublisher.Publish(new CheatUsedMessage(cheatUsed, playerData.Value.PlayerName));
            }
        }

#else
        void Awake()
        {
            m_DebugCheatsPanel.SetActive(false);
        }
#endif
    }
}
