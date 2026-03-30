using System;
using System.Collections.Generic;
using Mirror;
using Unity.BossRoom.Utils;
using UnityEngine;
using VContainer;

namespace Unity.BossRoom.ConnectionManagement
{
    public enum ConnectStatus
    {
        Undefined,
        Success,
        ServerFull,
        LoggedInAgain,
        UserRequestedDisconnect,
        GenericDisconnect,
        Reconnecting,
        IncompatibleBuildType,
        HostEndedSession,
        StartHostFailed,
        StartClientFailed
    }

    public struct ReconnectMessage
    {
        public int CurrentAttempt;
        public int MaxAttempt;

        public ReconnectMessage(int currentAttempt, int maxAttempt)
        {
            CurrentAttempt = currentAttempt;
            MaxAttempt = maxAttempt;
        }
    }

    public struct ConnectionEventMessage
    {
        public ConnectStatus ConnectStatus;
        public FixedPlayerName PlayerName;
    }

    [Serializable]
    public class ConnectionPayload
    {
        public string playerId;
        public string playerName;
        public bool isDebug;
        public string roomKey;
    }

    /// <summary>
    /// State machine that handles connection flow using Mirror Networking.
    /// Subscribes to BossRoomNetworkManager events instead of NGO callbacks.
    /// </summary>
    public class ConnectionManager : MonoBehaviour
    {
        ConnectionState m_CurrentState;

        [Inject]
        IObjectResolver m_Resolver;

        public int NbReconnectAttempts = 2;
        public int MaxConnectedPlayers = 8;

        /// <summary>Access to Mirror NetworkManager via our custom subclass.</summary>
        public BossRoomNetworkManager NetworkManager => BossRoomNetworkManager.singleton;

        internal readonly OfflineState m_Offline = new OfflineState();
        internal readonly ClientConnectingState m_ClientConnecting = new ClientConnectingState();
        internal readonly ClientConnectedState m_ClientConnected = new ClientConnectedState();
        internal readonly ClientReconnectingState m_ClientReconnecting = new ClientReconnectingState();
        internal readonly StartingHostState m_StartingHost = new StartingHostState();
        internal readonly HostingState m_Hosting = new HostingState();

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            List<ConnectionState> states = new() { m_Offline, m_ClientConnecting, m_ClientConnected, m_ClientReconnecting, m_StartingHost, m_Hosting };
            foreach (var connectionState in states)
            {
                m_Resolver.Inject(connectionState);
            }

            m_CurrentState = m_Offline;

            // Subscribe to BossRoomNetworkManager events
            var nm = BossRoomNetworkManager.singleton;
            if (nm == null)
                nm = FindObjectOfType<BossRoomNetworkManager>();
            if (nm != null)
            {
                nm.OnServerClientConnected += OnServerClientConnected;
                nm.OnServerClientDisconnected += OnServerClientDisconnected;
                nm.OnLocalClientConnected += OnLocalClientConnected;
                nm.OnLocalClientDisconnected += OnLocalClientDisconnected;
                nm.OnServerStartedEvent += OnServerStarted;
                nm.OnServerStoppedEvent += OnServerStopped;
                nm.OnTransportFailureEvent += OnTransportFailure;
            }
        }

        void OnDestroy()
        {
            var nm = BossRoomNetworkManager.singleton;
            if (nm == null)
                nm = FindObjectOfType<BossRoomNetworkManager>();
            if (nm != null)
            {
                nm.OnServerClientConnected -= OnServerClientConnected;
                nm.OnServerClientDisconnected -= OnServerClientDisconnected;
                nm.OnLocalClientConnected -= OnLocalClientConnected;
                nm.OnLocalClientDisconnected -= OnLocalClientDisconnected;
                nm.OnServerStartedEvent -= OnServerStarted;
                nm.OnServerStoppedEvent -= OnServerStopped;
                nm.OnTransportFailureEvent -= OnTransportFailure;
            }
        }

        internal void ChangeState(ConnectionState nextState)
        {
            Debug.Log($"{name}: Changed connection state from {m_CurrentState.GetType().Name} to {nextState.GetType().Name}.");

            if (m_CurrentState != null)
                m_CurrentState.Exit();

            m_CurrentState = nextState;
            m_CurrentState.Enter();
        }

        // Server-side: a remote client became ready
        void OnServerClientConnected(NetworkConnectionToClient conn)
        {
            m_CurrentState.OnClientConnected((ulong)conn.connectionId);
        }

        // Server-side: a remote client disconnected
        void OnServerClientDisconnected(NetworkConnectionToClient conn)
        {
            m_CurrentState.OnClientDisconnect((ulong)conn.connectionId);
        }

        // Local client successfully connected to a server
        void OnLocalClientConnected()
        {
            m_CurrentState.OnClientConnected(0);
        }

        // Local client disconnected from a server
        void OnLocalClientDisconnected()
        {
            m_CurrentState.OnClientDisconnect(0);
        }

        void OnServerStarted()
        {
            m_CurrentState.OnServerStarted();
        }

        void OnServerStopped(bool _)
        {
            m_CurrentState.OnServerStopped();
        }

        void OnTransportFailure()
        {
            m_CurrentState.OnTransportFailure();
        }

        public void StartClientIp(string playerName, string ipaddress, int port)
        {
            m_CurrentState.StartClientIP(playerName, ipaddress, port);
        }

        public void StartHostIp(string playerName, string ipaddress, int port)
        {
            m_CurrentState.StartHostIP(playerName, ipaddress, port);
        }

        public void RequestShutdown()
        {
            m_CurrentState.OnUserRequestedShutdown();
        }
    }
}
