using System;
using System.Collections;
using Mirror;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Infrastructure;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace Unity.BossRoom.Gameplay.GameState
{
    /// <summary>
    /// Server specialization of Character Select game state.
    /// </summary>
    [RequireComponent(typeof(NetworkHooks), typeof(NetworkCharSelection))]
    public class ServerCharSelectState : GameStateBehaviour
    {
        [FormerlySerializedAs("m_NetcodeHooks")]
        [SerializeField]
        NetworkHooks m_NetworkHooks;

        public override GameState ActiveState => GameState.CharSelect;
        public NetworkCharSelection networkCharSelection { get; private set; }

        Coroutine m_WaitToEndSessionCoroutine;

        [Inject]
        ConnectionManager m_ConnectionManager;

        [Inject]
        PersistentGameState m_PersistentGameState;

        protected override void Awake()
        {
            base.Awake();
            networkCharSelection = GetComponent<NetworkCharSelection>();

            if (m_NetworkHooks == null)
            {
                m_NetworkHooks = GetComponent<NetworkHooks>();
            }

            m_NetworkHooks.OnNetworkSpawn += OnNetworkSpawn;
            m_NetworkHooks.OnNetworkDespawn += OnNetworkDespawn;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (m_NetworkHooks)
            {
                m_NetworkHooks.OnNetworkSpawn -= OnNetworkSpawn;
                m_NetworkHooks.OnNetworkDespawn -= OnNetworkDespawn;
            }

            // Safety: unsubscribe from BossRoomNetworkManager events in case
            // OnNetworkDespawn didn't fire before this object was destroyed.
            if (BossRoomNetworkManager.singleton)
            {
                BossRoomNetworkManager.singleton.OnServerClientDisconnected -= OnServerClientDisconnected;
                BossRoomNetworkManager.singleton.OnServerClientConnected -= OnServerClientConnected;
            }
            if (networkCharSelection)
            {
                networkCharSelection.OnClientChangedSeat -= OnClientChangedSeat;
                networkCharSelection.OnClientChangedName -= OnClientChangedName;
            }
        }

        void OnClientChangedName(ulong clientId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return;

            // Update in session player list (for display in CharSelect UI)
            int idx = FindSessionPlayerIdx(clientId);
            if (idx != -1)
            {
                var old = networkCharSelection.sessionPlayers[idx];
                networkCharSelection.sessionPlayers[idx] = new NetworkCharSelection.SessionPlayerState(
                    old.ClientId, newName, old.PlayerNumber, old.SeatState, old.SeatIdx, old.LastChangeTime);
            }

            // Update SessionPlayerData so the name persists to gameplay
            var sessionData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(clientId);
            if (sessionData.HasValue)
            {
                var data = sessionData.Value;
                data.PlayerName = newName;
                SessionManager<SessionPlayerData>.Instance.SetPlayerData(clientId, data);
            }

            // Update PersistentPlayer's NetworkNameState
            if (NetworkServer.connections.TryGetValue((int)clientId, out var conn) && conn.identity != null)
            {
                if (conn.identity.TryGetComponent(out PersistentPlayer persistentPlayer))
                {
                    persistentPlayer.NetworkNameState.SetName(newName);
                }
            }
        }

        void OnClientChangedSeat(ulong clientId, int newSeatIdx, bool lockedIn)
        {
            int idx = FindSessionPlayerIdx(clientId);
            if (idx == -1)
            {
                throw new Exception($"OnClientChangedSeat: client ID {clientId} is not a Session player and cannot change seats! Shouldn't be here!");
            }

            if (networkCharSelection.IsSessionClosed)
            {
                // The user tried to change their class after everything was locked in... too late!
                return;
            }

            if (newSeatIdx == -1)
            {
                lockedIn = false;
            }
            else
            {
                foreach (NetworkCharSelection.SessionPlayerState playerInfo in networkCharSelection.sessionPlayers)
                {
                    if (playerInfo.ClientId != clientId && playerInfo.SeatIdx == newSeatIdx && playerInfo.SeatState == NetworkCharSelection.SeatState.LockedIn)
                    {
                        networkCharSelection.sessionPlayers[idx] = new NetworkCharSelection.SessionPlayerState(clientId,
                            networkCharSelection.sessionPlayers[idx].PlayerName,
                            networkCharSelection.sessionPlayers[idx].PlayerNumber,
                            NetworkCharSelection.SeatState.Inactive);
                        return;
                    }
                }
            }

            networkCharSelection.sessionPlayers[idx] = new NetworkCharSelection.SessionPlayerState(clientId,
                networkCharSelection.sessionPlayers[idx].PlayerName,
                networkCharSelection.sessionPlayers[idx].PlayerNumber,
                lockedIn ? NetworkCharSelection.SeatState.LockedIn : NetworkCharSelection.SeatState.Active,
                newSeatIdx,
                Time.time);

            if (lockedIn)
            {
                for (int i = 0; i < networkCharSelection.sessionPlayers.Count; ++i)
                {
                    if (networkCharSelection.sessionPlayers[i].SeatIdx == newSeatIdx && i != idx)
                    {
                        networkCharSelection.sessionPlayers[i] = new NetworkCharSelection.SessionPlayerState(
                            networkCharSelection.sessionPlayers[i].ClientId,
                            networkCharSelection.sessionPlayers[i].PlayerName,
                            networkCharSelection.sessionPlayers[i].PlayerNumber,
                            NetworkCharSelection.SeatState.Inactive);
                    }
                }
            }

            CloseSessionIfReady();
        }

        int FindSessionPlayerIdx(ulong clientId)
        {
            for (int i = 0; i < networkCharSelection.sessionPlayers.Count; ++i)
            {
                if (networkCharSelection.sessionPlayers[i].ClientId == clientId)
                    return i;
            }
            return -1;
        }

        void CloseSessionIfReady()
        {
            foreach (NetworkCharSelection.SessionPlayerState playerInfo in networkCharSelection.sessionPlayers)
            {
                if (playerInfo.SeatState != NetworkCharSelection.SeatState.LockedIn)
                    return;
            }

            networkCharSelection.IsSessionClosed = true;

            SaveSessionResults();

            m_WaitToEndSessionCoroutine = StartCoroutine(WaitToEndSession());
        }

        void CancelCloseSession()
        {
            if (m_WaitToEndSessionCoroutine != null)
            {
                StopCoroutine(m_WaitToEndSessionCoroutine);
            }
            networkCharSelection.IsSessionClosed = false;
        }

        void SaveSessionResults()
        {
            foreach (NetworkCharSelection.SessionPlayerState playerInfo in networkCharSelection.sessionPlayers)
            {
                if (NetworkServer.connections.TryGetValue((int)playerInfo.ClientId, out var conn) && conn.identity != null)
                {
                    if (conn.identity.TryGetComponent(out PersistentPlayer persistentPlayer))
                    {
                        persistentPlayer.NetworkAvatarGuidState.AvatarGuid =
                            networkCharSelection.AvatarConfiguration[playerInfo.SeatIdx].Guid.ToNetworkGuid();
                    }
                }
            }
        }

        IEnumerator WaitToEndSession()
        {
            yield return new WaitForSeconds(3);
            SceneLoaderWrapper.Instance.LoadScene("BossRoom", useNetworkSceneManager: true);
        }

        void OnNetworkDespawn()
        {
            if (BossRoomNetworkManager.singleton)
            {
                BossRoomNetworkManager.singleton.OnServerClientDisconnected -= OnServerClientDisconnected;
                BossRoomNetworkManager.singleton.OnServerClientConnected -= OnServerClientConnected;
            }
            if (networkCharSelection)
            {
                networkCharSelection.OnClientChangedSeat -= OnClientChangedSeat;
                networkCharSelection.OnClientChangedName -= OnClientChangedName;
            }
        }

        void OnNetworkSpawn()
        {
            if (!NetworkServer.active)
            {
                enabled = false;
            }
            else
            {
                BossRoomNetworkManager.singleton.OnServerClientDisconnected += OnServerClientDisconnected;
                BossRoomNetworkManager.singleton.OnServerClientConnected += OnServerClientConnected;
                networkCharSelection.OnClientChangedSeat += OnClientChangedSeat;
                networkCharSelection.OnClientChangedName += OnClientChangedName;

                // Push previous match result to clients if returning from a PvP match
                if (m_PersistentGameState != null && m_PersistentGameState.HasMatchResult)
                {
                    var result = new MatchResult
                    {
                        winnerName = m_PersistentGameState.WinnerName,
                        winnerClientId = (long)m_PersistentGameState.WinnerClientId
                    };
                    networkCharSelection.MatchResultJson = JsonUtility.ToJson(result);
                    m_PersistentGameState.ClearMatchResult();
                }

                // Seat any connections that are already present (e.g. the host's local connection,
                // which became ready before this state object was spawned).
                foreach (var kvp in NetworkServer.connections)
                {
                    if (kvp.Value != null && kvp.Value.isReady)
                    {
                        SeatNewPlayer((ulong)kvp.Key);
                    }
                }
            }
        }

        /// <summary>
        /// Called when a client finishes loading the scene and is ready — seat them in the session.
        /// </summary>
        void OnServerClientConnected(NetworkConnectionToClient conn)
        {
            if (this == null) return; // guard against callbacks on destroyed object
            SeatNewPlayer((ulong)conn.connectionId);
        }

        void OnServerClientDisconnected(NetworkConnectionToClient conn)
        {
            ulong clientId = (ulong)conn.connectionId;
            for (int i = 0; i < networkCharSelection.sessionPlayers.Count; ++i)
            {
                if (networkCharSelection.sessionPlayers[i].ClientId == clientId)
                {
                    networkCharSelection.sessionPlayers.RemoveAt(i);
                    break;
                }
            }

            if (!networkCharSelection.IsSessionClosed)
            {
                CloseSessionIfReady();
            }
        }

        int GetAvailablePlayerNumber()
        {
            for (int possiblePlayerNumber = 0; possiblePlayerNumber < m_ConnectionManager.MaxConnectedPlayers; ++possiblePlayerNumber)
            {
                if (IsPlayerNumberAvailable(possiblePlayerNumber))
                {
                    return possiblePlayerNumber;
                }
            }
            return -1;
        }

        bool IsPlayerNumberAvailable(int playerNumber)
        {
            bool found = false;
            foreach (NetworkCharSelection.SessionPlayerState playerState in networkCharSelection.sessionPlayers)
            {
                if (playerState.PlayerNumber == playerNumber)
                {
                    found = true;
                    break;
                }
            }

            return !found;
        }

        void SeatNewPlayer(ulong clientId)
        {
            // Prevent double-seating the same client (can happen when OnServerReady fires
            // after a scene change for an already-seated connection).
            foreach (var existing in networkCharSelection.sessionPlayers)
            {
                if (existing.ClientId == clientId)
                {
                    return;
                }
            }

            if (networkCharSelection.IsSessionClosed)
            {
                CancelCloseSession();
            }

            SessionPlayerData? sessionPlayerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(clientId);
            if (sessionPlayerData.HasValue)
            {
                var playerData = sessionPlayerData.Value;
                if (playerData.PlayerNumber == -1 || !IsPlayerNumberAvailable(playerData.PlayerNumber))
                {
                    playerData.PlayerNumber = GetAvailablePlayerNumber();
                }
                if (playerData.PlayerNumber == -1)
                {
                    throw new Exception($"we shouldn't be here, connection approval should have refused this connection already for client ID {clientId} and player num {playerData.PlayerNumber}");
                }

                networkCharSelection.sessionPlayers.Add(new NetworkCharSelection.SessionPlayerState(clientId, playerData.PlayerName, playerData.PlayerNumber, NetworkCharSelection.SeatState.Inactive));
                SessionManager<SessionPlayerData>.Instance.SetPlayerData(clientId, playerData);
            }
        }
    }
}
