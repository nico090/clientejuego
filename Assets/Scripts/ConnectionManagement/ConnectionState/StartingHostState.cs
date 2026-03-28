using Unity.BossRoom.Infrastructure;
using UnityEngine;

namespace Unity.BossRoom.ConnectionManagement
{
    /// <summary>
    /// Connection state corresponding to a host starting up. Starts the host when entering the state. If successful,
    /// transitions to the Hosting state, if not, transitions back to the Offline state.
    /// </summary>
    class StartingHostState : OnlineState
    {
        ConnectionMethodBase m_ConnectionMethod;

        public StartingHostState Configure(ConnectionMethodBase baseConnectionMethod)
        {
            m_ConnectionMethod = baseConnectionMethod;
            return this;
        }

        public override void Enter()
        {
            StartHost();
        }

        public override void Exit() { }

        public override void OnServerStarted()
        {
            m_ConnectStatusPublisher.Publish(ConnectStatus.Success);
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Hosting);
        }

        public override void OnServerStopped()
        {
            StartHostFailed();
        }

        void StartHost()
        {
            if (m_ConnectionManager.NetworkManager == null)
            {
                Debug.LogError("BossRoomNetworkManager singleton is null. Make sure a BossRoomNetworkManager component exists in the scene.");
                StartHostFailed();
                return;
            }

            if (m_ConnectionMethod == null)
            {
                Debug.LogError("StartingHostState: ConnectionMethod is null. Was Configure() called before Enter()?");
                StartHostFailed();
                return;
            }

            m_ConnectionMethod.SetupHostConnection();

            // Register the host's session data before StartHost() triggers callbacks,
            // because OnClientConnect fires before the ConnectionPayloadMessage is processed.
            var payload = m_ConnectionManager.NetworkManager.PendingClientPayload;
            if (payload != null)
            {
                ulong hostClientId = 0; // Mirror host connection is always 0
                SessionManager<SessionPlayerData>.Instance.SetupConnectingPlayerSessionData(
                    hostClientId,
                    payload.playerId,
                    new SessionPlayerData(hostClientId, payload.playerName, default, 0, true, false));
            }

            // Mirror StartHost — if it fails OnServerStopped will fire
            m_ConnectionManager.NetworkManager.StartHost();
        }

        void StartHostFailed()
        {
            m_ConnectStatusPublisher.Publish(ConnectStatus.StartHostFailed);
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
        }
    }
}
