using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Gameplay.Messages;
using Unity.BossRoom.Infrastructure;
using Unity.BossRoom.Utils;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using VContainer;
using Random = UnityEngine.Random;

namespace Unity.BossRoom.Gameplay.GameState
{
    /// <summary>
    /// Server specialization of core BossRoom game logic.
    /// </summary>
    [RequireComponent(typeof(NetworkHooks))]
    public class ServerBossRoomState : GameStateBehaviour
    {
        [FormerlySerializedAs("m_NetworkWinState")]
        [SerializeField]
        PersistentGameState persistentGameState;

        [FormerlySerializedAs("m_NetcodeHooks")]
        [SerializeField]
        NetworkHooks m_NetworkHooks;

        [SerializeField]
        [Tooltip("Make sure this prefab has a NetworkIdentity component!")]
        private GameObject m_PlayerPrefab;

        [SerializeField]
        [Tooltip("A collection of locations for spawning players")]
        private Transform[] m_PlayerSpawnPoints;

        private List<Transform> m_PlayerSpawnPointsList = null;

        public override GameState ActiveState { get { return GameState.BossRoom; } }

        private const float k_PostMatchDelay = 7.0f;
        private const float k_RespawnDelay = 5.0f;

        /// <summary>
        /// Has the ServerBossRoomState already hit its initial spawn?
        /// </summary>
        public bool InitialSpawnDone { get; private set; }

        [Inject] ISubscriber<LifeStateChangedEventMessage> m_LifeStateChangedEventMessageSubscriber;

        [Inject] ConnectionManager m_ConnectionManager;
        [Inject] PersistentGameState m_PersistentGameState;

        protected override void Awake()
        {
            base.Awake();
            m_NetworkHooks.OnNetworkSpawn += OnNetworkSpawn;
            m_NetworkHooks.OnNetworkDespawn += OnNetworkDespawn;
        }

        void OnNetworkSpawn()
        {
            if (!NetworkServer.active)
            {
                enabled = false;
                return;
            }
            m_PersistentGameState.Reset();
            m_LifeStateChangedEventMessageSubscriber.Subscribe(OnLifeStateChangedEventMessage);

            BossRoomNetworkManager.singleton.OnServerClientDisconnected += OnServerClientDisconnected;
            BossRoomNetworkManager.singleton.OnServerSceneChangedEvent += OnServerSceneChanged;
            BossRoomNetworkManager.singleton.OnServerClientConnected += OnServerClientConnected;

            // Ensure PvPScoreManager exists on this same networked GameObject
            EnsurePvPScoreManager();

            SessionManager<SessionPlayerData>.Instance.OnSessionStarted();
        }

        void EnsurePvPScoreManager()
        {
            if (PvPScoreManager.Instance == null)
            {
                // Add to this GameObject which already has NetworkIdentity
                gameObject.AddComponent<PvPScoreManager>();
            }
            PvPScoreManager.Instance.MatchEnded += OnPvPMatchEnded;
        }

        void OnNetworkDespawn()
        {
            if (m_LifeStateChangedEventMessageSubscriber != null)
            {
                m_LifeStateChangedEventMessageSubscriber.Unsubscribe(OnLifeStateChangedEventMessage);
            }

            if (PvPScoreManager.Instance != null)
            {
                PvPScoreManager.Instance.MatchEnded -= OnPvPMatchEnded;
            }

            if (BossRoomNetworkManager.singleton)
            {
                BossRoomNetworkManager.singleton.OnServerClientDisconnected -= OnServerClientDisconnected;
                BossRoomNetworkManager.singleton.OnServerSceneChangedEvent -= OnServerSceneChanged;
                BossRoomNetworkManager.singleton.OnServerClientConnected -= OnServerClientConnected;
            }
        }

        protected override void OnDestroy()
        {
            if (m_LifeStateChangedEventMessageSubscriber != null)
            {
                m_LifeStateChangedEventMessageSubscriber.Unsubscribe(OnLifeStateChangedEventMessage);
            }

            if (PvPScoreManager.Instance != null)
            {
                PvPScoreManager.Instance.MatchEnded -= OnPvPMatchEnded;
            }

            if (m_NetworkHooks)
            {
                m_NetworkHooks.OnNetworkSpawn -= OnNetworkSpawn;
                m_NetworkHooks.OnNetworkDespawn -= OnNetworkDespawn;
            }

            if (BossRoomNetworkManager.singleton)
            {
                BossRoomNetworkManager.singleton.OnServerClientDisconnected -= OnServerClientDisconnected;
                BossRoomNetworkManager.singleton.OnServerSceneChangedEvent -= OnServerSceneChanged;
                BossRoomNetworkManager.singleton.OnServerClientConnected -= OnServerClientConnected;
            }

            base.OnDestroy();
        }

        /// <summary>
        /// Called when a client finishes synchronizing — handles late-join scenario.
        /// </summary>
        void OnServerClientConnected(NetworkConnectionToClient conn)
        {
            if (this == null) return; // guard against callbacks on destroyed object
            ulong clientId = (ulong)conn.connectionId;
            if (InitialSpawnDone && !PlayerServerCharacter.GetPlayerServerCharacter(clientId))
            {
                SpawnPlayer(clientId, true);
            }
        }

        /// <summary>
        /// Called when the server finishes loading the new scene — triggers initial player spawn.
        /// </summary>
        void OnServerSceneChanged(string sceneName)
        {
            if (!InitialSpawnDone)
            {
                InitialSpawnDone = true;
                foreach (var kvp in NetworkServer.connections)
                {
                    SpawnPlayer((ulong)kvp.Key, false);
                }

                // Start the PvP match after all players have spawned
                if (PvPScoreManager.Instance != null)
                {
                    PvPScoreManager.Instance.StartMatch();
                }
            }
        }

        void OnServerClientDisconnected(NetworkConnectionToClient conn)
        {
            // Unregister the disconnected player's avatar from the PvP score system
            if (PvPScoreManager.Instance != null && conn != null)
            {
                foreach (var owned in conn.owned)
                {
                    if (owned != null && owned != conn.identity && owned.TryGetComponent<ServerCharacter>(out _))
                    {
                        PvPScoreManager.Instance.UnregisterPlayer(owned.netId);
                        break;
                    }
                }
            }
        }

        void SpawnPlayer(ulong clientId, bool lateJoin)
        {
            Transform spawnPoint = null;

            if (m_PlayerSpawnPointsList == null || m_PlayerSpawnPointsList.Count == 0)
            {
                m_PlayerSpawnPointsList = new List<Transform>(m_PlayerSpawnPoints);
            }

            Debug.Assert(m_PlayerSpawnPointsList.Count > 0,
                $"PlayerSpawnPoints array should have at least 1 spawn points.");

            int index = Random.Range(0, m_PlayerSpawnPointsList.Count);
            spawnPoint = m_PlayerSpawnPointsList[index];
            m_PlayerSpawnPointsList.RemoveAt(index);

            // Get the player's persistent object (PersistentPlayer) from their connection
            if (!NetworkServer.connections.TryGetValue((int)clientId, out var conn) || conn == null)
            {
                Debug.LogWarning($"[ServerBossRoomState] SpawnPlayer: connection {clientId} not found, aborting spawn.");
                return;
            }

            GameObject persistentPlayerGO = conn.identity != null ? conn.identity.gameObject : null;

            var newPlayer = Instantiate(m_PlayerPrefab, Vector3.zero, Quaternion.identity);

            var newPlayerCharacter = newPlayer.GetComponent<ServerCharacter>();

            var physicsTransform = newPlayerCharacter.physicsWrapper.Transform;

            if (spawnPoint != null)
            {
                physicsTransform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            }

            PersistentPlayer persistentPlayer = null;
            var persistentPlayerExists = persistentPlayerGO != null &&
                persistentPlayerGO.TryGetComponent(out persistentPlayer);
            Assert.IsTrue(persistentPlayerExists,
                $"Matching persistent PersistentPlayer for client {clientId} not found!");

            var networkAvatarGuidStateExists =
                newPlayer.TryGetComponent(out NetworkAvatarGuidState networkAvatarGuidState);

            Assert.IsTrue(networkAvatarGuidStateExists,
                $"NetworkCharacterGuidState not found on player avatar!");

            if (lateJoin)
            {
                SessionPlayerData? sessionPlayerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(clientId);
                if (sessionPlayerData is { HasCharacterSpawned: true })
                {
                    physicsTransform.SetPositionAndRotation(sessionPlayerData.Value.PlayerPosition, sessionPlayerData.Value.PlayerRotation);
                }
            }

            // Copy avatar GUID and name from PersistentPlayer before spawning
            networkAvatarGuidState.AvatarGuid = persistentPlayer.NetworkAvatarGuidState.AvatarGuid;

            if (newPlayer.TryGetComponent(out NetworkNameState networkNameState))
            {
                networkNameState.SetName(persistentPlayer.NetworkNameState.Name);
            }

            // Spawn with ownership assigned to the client's connection
            NetworkServer.Spawn(newPlayer, conn);

            // Register player in PvP score system
            if (PvPScoreManager.Instance != null)
            {
                var netId = newPlayer.GetComponent<NetworkIdentity>().netId;
                string playerName = networkNameState != null
                    ? (string)networkNameState.Name
                    : $"Player {netId}";
                PvPScoreManager.Instance.RegisterPlayer(netId, playerName);
            }
        }

        void OnLifeStateChangedEventMessage(LifeStateChangedEventMessage message)
        {
            switch (message.CharacterType)
            {
                case CharacterTypeEnum.Tank:
                case CharacterTypeEnum.Archer:
                case CharacterTypeEnum.Mage:
                case CharacterTypeEnum.Rogue:
                    if (message.NewLifeState == LifeState.Fainted)
                    {
                        // PvP: respawn player after delay
                        var serverChar = message.ServerCharacter;
                        if (serverChar != null)
                        {
                            StartCoroutine(CoroRespawnPlayer(serverChar));
                        }
                    }
                    break;
                case CharacterTypeEnum.ImpBoss:
                    // NPCs dying is normal in PvP — no special handling needed
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        IEnumerator CoroRespawnPlayer(ServerCharacter serverCharacter)
        {
            yield return new WaitForSeconds(k_RespawnDelay);

            if (serverCharacter == null || serverCharacter.LifeState != LifeState.Fainted)
                yield break;

            // Pick a random spawn point
            Transform spawnPoint = m_PlayerSpawnPoints[Random.Range(0, m_PlayerSpawnPoints.Length)];

            // Reposition at spawn point
            var physicsTransform = serverCharacter.physicsWrapper.Transform;
            physicsTransform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

            // Revive with full HP
            serverCharacter.Revive(null, serverCharacter.CharacterClass.BaseHP.Value);
        }

        void OnPvPMatchEnded()
        {
            // Store winner info for CharSelect to display
            if (PvPScoreManager.Instance != null)
            {
                string winnerName = PvPScoreManager.Instance.GetWinnerName();
                uint winnerNetId = PvPScoreManager.Instance.GetWinnerNetId();

                // Find the connection ID (clientId) for the winner by netId
                ulong winnerClientId = 0;
                foreach (var kvp in NetworkServer.connections)
                {
                    if (kvp.Value?.identity != null)
                    {
                        // Check owned objects for the player avatar with matching netId
                        foreach (var owned in kvp.Value.owned)
                        {
                            if (owned.netId == winnerNetId)
                            {
                                winnerClientId = (ulong)kvp.Key;
                                break;
                            }
                        }
                    }
                }

                m_PersistentGameState.SetMatchResult(winnerName, winnerClientId);
            }

            StartCoroutine(CoroGoToCharSelect());
        }

        IEnumerator CoroGoToCharSelect()
        {
            yield return new WaitForSeconds(k_PostMatchDelay);
            SceneLoaderWrapper.Instance.LoadScene("CharSelect", useNetworkSceneManager: true);
        }
    }
}
