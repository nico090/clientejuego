using System.Threading.Tasks;
using Unity.BossRoom.Utils;
using UnityEngine;

namespace Unity.BossRoom.ConnectionManagement
{
    /// <summary>
    /// ConnectionMethod contains all setup needed to configure Mirror networking before starting a
    /// connection (either host or client side).
    /// </summary>
    public abstract class ConnectionMethodBase
    {
        protected ConnectionManager m_ConnectionManager;
        protected readonly string m_PlayerName;

        public abstract void SetupHostConnection();
        public abstract void SetupClientConnection();
        public abstract Task<(bool success, bool shouldTryAgain)> SetupClientReconnectionAsync();

        public ConnectionMethodBase(ConnectionManager connectionManager, string playerName)
        {
            m_ConnectionManager = connectionManager;
            m_PlayerName = playerName;
        }

        protected void SetConnectionPayload(string playerId, string playerName, string roomKey = null)
        {
            // Preserve any existing roomKey if one isn't explicitly provided
            var existing = m_ConnectionManager.NetworkManager.PendingClientPayload;
            m_ConnectionManager.NetworkManager.PendingClientPayload = new ConnectionPayload
            {
                playerId = playerId,
                playerName = playerName,
                isDebug = Debug.isDebugBuild,
                roomKey = roomKey ?? existing?.roomKey
            };
        }

        protected string GetPlayerId()
        {
            return ClientPrefs.GetGuid();
        }
    }

    /// <summary>
    /// Simple IP connection setup using Mirror transport.
    /// </summary>
    class ConnectionMethodIP : ConnectionMethodBase
    {
        string m_Ipaddress;
        ushort m_Port;

        public ConnectionMethodIP(string ip, ushort port, ConnectionManager connectionManager, string playerName)
            : base(connectionManager, playerName)
        {
            m_Ipaddress = ip;
            m_Port = port;
        }

        public override void SetupClientConnection()
        {
            SetConnectionPayload(GetPlayerId(), m_PlayerName);
            m_ConnectionManager.NetworkManager.SetNetworkAddress(m_Ipaddress, m_Port);
        }

        public override Task<(bool success, bool shouldTryAgain)> SetupClientReconnectionAsync()
        {
            return Task.FromResult((true, true));
        }

        public override void SetupHostConnection()
        {
            SetConnectionPayload(GetPlayerId(), m_PlayerName);
            m_ConnectionManager.NetworkManager.SetNetworkAddress(m_Ipaddress, m_Port);
        }
    }
}
