using Unity.BossRoom.Infrastructure;
using UnityEngine;
using VContainer;

namespace Unity.BossRoom.ConnectionManagement
{
    /// <summary>
    /// Connection state corresponding to a listening host. Handles incoming client connections.
    /// </summary>
    class HostingState : OnlineState
    {
        [Inject]
        IPublisher<ConnectionEventMessage> m_ConnectionEventPublisher;

        public override void Enter()
        {
            SceneLoaderWrapper.Instance.LoadScene("CharSelect", useNetworkSceneManager: true);
        }

        public override void Exit()
        {
            SessionManager<SessionPlayerData>.Instance.OnServerEnded();
        }

        public override void OnClientConnected(ulong clientId)
        {
            var playerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(clientId);
            if (playerData != null)
            {
                m_ConnectionEventPublisher.Publish(new ConnectionEventMessage()
                {
                    ConnectStatus = ConnectStatus.Success,
                    PlayerName = playerData.Value.PlayerName
                });
            }
            else
            {
                Debug.LogError($"No player data associated with client {clientId}");
                var reason = JsonUtility.ToJson(ConnectStatus.GenericDisconnect);
                m_ConnectionManager.NetworkManager.DisconnectClient((int)clientId, reason);
            }
        }

        public override void OnClientDisconnect(ulong clientId)
        {
            if (clientId != 0) // 0 = local host connection
            {
                var playerId = SessionManager<SessionPlayerData>.Instance.GetPlayerId(clientId);
                if (playerId != null)
                {
                    var sessionData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(playerId);
                    if (sessionData.HasValue)
                    {
                        m_ConnectionEventPublisher.Publish(new ConnectionEventMessage()
                        {
                            ConnectStatus = ConnectStatus.GenericDisconnect,
                            PlayerName = sessionData.Value.PlayerName
                        });
                    }
                    SessionManager<SessionPlayerData>.Instance.DisconnectClient(clientId);
                }
            }
        }

        public override void OnUserRequestedShutdown()
        {
            var reason = JsonUtility.ToJson(ConnectStatus.HostEndedSession);
            foreach (var conn in m_ConnectionManager.NetworkManager.GetRemoteConnections())
            {
                m_ConnectionManager.NetworkManager.DisconnectClient(conn.connectionId, reason);
            }
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
        }

        public override void OnServerStopped()
        {
            m_ConnectStatusPublisher.Publish(ConnectStatus.GenericDisconnect);
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
        }
    }
}
