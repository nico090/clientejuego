using System.Collections.Generic;

namespace Unity.BossRoom.Infrastructure
{
    /// <summary>
    /// Plain C# singleton that tracks session data for connected players.
    /// Not a MonoBehaviour — Unity does not support AddComponent on generic MonoBehaviour classes,
    /// and this class only stores data so it does not need Unity lifecycle callbacks.
    /// </summary>
    public class SessionManager<T> where T : struct
    {
        private static SessionManager<T> s_Instance;

        public static SessionManager<T> Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = new SessionManager<T>();
                }
                return s_Instance;
            }
        }

        private readonly Dictionary<ulong, T> m_ClientData = new Dictionary<ulong, T>();
        private readonly Dictionary<ulong, string> m_ClientIdToPlayerId = new Dictionary<ulong, string>();
        private readonly Dictionary<string, ulong> m_PlayerIdToClientId = new Dictionary<string, ulong>();

        public void SetupConnectingPlayerSessionData(ulong clientId, string playerId, T data)
        {
            m_ClientData[clientId] = data;
            m_ClientIdToPlayerId[clientId] = playerId;
            m_PlayerIdToClientId[playerId] = clientId;
        }

        public T? GetPlayerData(ulong clientId)
        {
            if (m_ClientData.TryGetValue(clientId, out T data))
                return data;
            return null;
        }

        public T? GetPlayerData(string playerId)
        {
            if (m_PlayerIdToClientId.TryGetValue(playerId, out ulong clientId))
                return GetPlayerData(clientId);
            return null;
        }

        public string GetPlayerId(ulong clientId)
        {
            return m_ClientIdToPlayerId.TryGetValue(clientId, out string playerId) ? playerId : null;
        }

        public void DisconnectClient(ulong clientId)
        {
            m_ClientData.Remove(clientId);
        }

        public void SetPlayerData(ulong clientId, T data)
        {
            m_ClientData[clientId] = data;
        }

        public void OnSessionStarted() { }

        public void OnSessionEnded()
        {
            m_ClientData.Clear();
            m_ClientIdToPlayerId.Clear();
            m_PlayerIdToClientId.Clear();
        }

        public void OnServerEnded()
        {
            m_ClientData.Clear();
            m_ClientIdToPlayerId.Clear();
            m_PlayerIdToClientId.Clear();
        }
    }
}
