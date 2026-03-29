using System.Collections.Generic;
using Mirror;
using Unity.BossRoom.Infrastructure;
using UnityEngine;

namespace Unity.BossRoom.ConnectionManagement
{
    /// <summary>
    /// Mirror NetworkManager subclass that exposes connection lifecycle events
    /// for use by the ConnectionManager state machine, and handles the connection
    /// payload exchange (player name / ID) that NGO used to do via ConnectionApprovalCallback.
    /// </summary>
    public class BossRoomNetworkManager : NetworkManager
    {
        public static new BossRoomNetworkManager singleton =>
            NetworkManager.singleton as BossRoomNetworkManager;

        // Server-side events (server sees clients connect/disconnect)
        public System.Action<NetworkConnectionToClient> OnServerClientConnected;
        public System.Action<NetworkConnectionToClient> OnServerClientDisconnected;

        // Local client events (this peer connected/disconnected from a server)
        public System.Action OnLocalClientConnected;
        public System.Action OnLocalClientDisconnected;

        // Server lifecycle
        public System.Action OnServerStartedEvent;
        public System.Action<bool> OnServerStoppedEvent;

        // Transport failure
        public System.Action OnTransportFailureEvent;

        /// <summary>Last disconnect reason received from the server before disconnection.</summary>
        public string DisconnectReason { get; private set; } = string.Empty;

        public void ClearDisconnectReason() => DisconnectReason = string.Empty;

        /// <summary>
        /// Connection payload set by the client before calling StartClient/StartHost.
        /// Sent to the server right after the TCP connection is established.
        /// </summary>
        public ConnectionPayload PendingClientPayload { get; set; }

        // ---- Mirror overrides ----

        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkServer.RegisterHandler<ConnectionPayloadMessage>(OnReceiveConnectionPayload);
            OnServerStartedEvent?.Invoke();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            OnServerStoppedEvent?.Invoke(false);

            // Clear all delegates to prevent leaked references to destroyed scene objects.
            OnServerClientConnected = null;
            OnServerClientDisconnected = null;
            OnServerSceneChangedEvent = null;
            OnClientSceneChangedEvent = null;
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
        }

        /// <summary>Called on server when a remote client finishes connecting and is ready.</summary>
        public override void OnServerReady(NetworkConnectionToClient conn)
        {
            base.OnServerReady(conn);
            OnServerClientConnected?.Invoke(conn);
        }

        /// <summary>
        /// Called on server when client requests a player object.
        /// Skips creation if the connection already has a player (e.g. after scene change).
        /// </summary>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            if (conn.identity != null) return; // already has a PersistentPlayer from a previous scene
            base.OnServerAddPlayer(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            OnServerClientDisconnected?.Invoke(conn);
            base.OnServerDisconnect(conn);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            NetworkClient.RegisterHandler<DisconnectReasonMessage>(OnReceiveDisconnectReason, false);
        }

        public override void OnClientConnect()
        {
            // Send connection payload to server BEFORE ready message (TCP order is guaranteed)
            if (PendingClientPayload != null)
            {
                NetworkClient.Send(new ConnectionPayloadMessage
                {
                    PayloadJson = JsonUtility.ToJson(PendingClientPayload)
                });
            }
            base.OnClientConnect();
            OnLocalClientConnected?.Invoke();
        }

        public override void OnClientDisconnect()
        {
            OnLocalClientDisconnected?.Invoke();
            base.OnClientDisconnect();
        }

        /// <summary>Fired on the server after it finishes loading a new scene.</summary>
        public System.Action<string> OnServerSceneChangedEvent;

        /// <summary>Fired on the client after it finishes loading a new scene requested by the server.</summary>
        public System.Action<string> OnClientSceneChangedEvent;

        public override void OnServerSceneChanged(string newSceneName)
        {
            base.OnServerSceneChanged(newSceneName);
            OnServerSceneChangedEvent?.Invoke(newSceneName);
            SceneLoaderWrapper.Instance?.HandleServerSceneChanged(newSceneName);
        }

        public override void OnClientSceneChanged()
        {
            // Mark client as ready after a scene change (skip if already ready).
            if (!NetworkClient.ready)
                NetworkClient.Ready();

            // Do NOT call AddPlayer here. PersistentPlayer is created once
            // during initial connection (OnClientConnect → base.OnClientConnect)
            // and survives scene changes via DontDestroyOnLoad.

            OnClientSceneChangedEvent?.Invoke(networkSceneName);
            SceneLoaderWrapper.Instance?.HandleClientSceneChanged(networkSceneName);
        }

        void OnReceiveDisconnectReason(DisconnectReasonMessage msg)
        {
            DisconnectReason = msg.Reason;
        }

        void OnReceiveConnectionPayload(NetworkConnectionToClient conn, ConnectionPayloadMessage msg)
        {
            var payload = JsonUtility.FromJson<ConnectionPayload>(msg.PayloadJson);
            if (payload == null) return;

            var clientId = (ulong)conn.connectionId;
            var avatarGuid = default(NetworkGuid);
            SessionManager<SessionPlayerData>.Instance.SetupConnectingPlayerSessionData(
                clientId,
                payload.playerId,
                new SessionPlayerData(clientId, payload.playerName, avatarGuid, 0, true, false));
        }

        // ---- Helper API ----

        public List<NetworkConnectionToClient> GetRemoteConnections()
        {
            var result = new List<NetworkConnectionToClient>();
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn != NetworkServer.localConnection)
                    result.Add(conn);
            }
            return result;
        }

        public void DisconnectClient(int connectionId, string reason)
        {
            if (NetworkServer.connections.TryGetValue(connectionId, out var conn))
            {
                conn.Send(new DisconnectReasonMessage { Reason = reason });
                conn.Disconnect();
            }
        }

        /// <summary>Stops the network session regardless of current role.</summary>
        public void Shutdown()
        {
            if (mode == NetworkManagerMode.Host)
                StopHost();
            else if (mode == NetworkManagerMode.ServerOnly)
                StopServer();
            else if (mode == NetworkManagerMode.ClientOnly)
                StopClient();
        }

        /// <summary>
        /// Sets the network address and attempts to set the transport port on common Mirror transports.
        /// </summary>
        public void SetNetworkAddress(string address, ushort port)
        {
            networkAddress = address;
            SetTransportPort(port);
        }

        /// <summary>
        /// Attempts to set the port on the active transport. Supports kcp2k and Telepathy.
        /// </summary>
        public void SetTransportPort(ushort port)
        {
            if (transport == null) return;

            var type = transport.GetType();

            // Try common property names first
            foreach (string name in new[] { "Port", "port" })
            {
                var prop = type.GetProperty(name);
                if (prop != null && prop.PropertyType == typeof(ushort) && prop.CanWrite)
                {
                    prop.SetValue(transport, port);
                    Debug.Log($"[BossRoomNetworkManager] Set port {port} via property '{name}' on {type.Name}");
                    return;
                }
            }

            // Try common field names (KcpTransport uses a public field 'Port', Telepathy uses 'port')
            foreach (string name in new[] { "Port", "port" })
            {
                var field = type.GetField(name);
                if (field != null && field.FieldType == typeof(ushort))
                {
                    field.SetValue(transport, port);
                    Debug.Log($"[BossRoomNetworkManager] Set port {port} via field '{name}' on {type.Name}");
                    return;
                }
            }

            Debug.LogWarning($"BossRoomNetworkManager: could not set port {port} — transport type {type.Name} not recognized.");
        }
    }

    public struct DisconnectReasonMessage : NetworkMessage
    {
        public string Reason;
    }

    public struct ConnectionPayloadMessage : NetworkMessage
    {
        public string PayloadJson;
    }
}
