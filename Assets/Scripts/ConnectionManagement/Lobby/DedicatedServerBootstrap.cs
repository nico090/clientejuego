using System;
using System.Collections;
using System.Linq;
using System.Text;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.BossRoom.ConnectionManagement.Lobby
{
    /// <summary>
    /// Bootstraps a dedicated (headless) game server.
    /// Reads command-line args, starts Mirror in server-only mode,
    /// and sends periodic heartbeats to the master server.
    /// Attach to a GameObject in the Startup scene.
    /// </summary>
    public class DedicatedServerBootstrap : MonoBehaviour
    {
        string m_RoomId;
        string m_ServerSecret;
        string m_MasterServerUrl;
        int m_Port = 7777;
        int m_MaxPlayers = 8;

        void Start()
        {
            if (!IsDedicatedServer())
            {
                Destroy(gameObject);
                return;
            }

            ParseCommandLineArgs();

            Debug.Log($"[DedicatedServer] Starting on port {m_Port}, room={m_RoomId}");

            var netManager = BossRoomNetworkManager.singleton;
            netManager.SetTransportPort((ushort)m_Port);
            netManager.maxConnections = m_MaxPlayers;
            netManager.StartServer();

            DontDestroyOnLoad(gameObject);

            StartCoroutine(HeartbeatLoop());
            StartCoroutine(EmptyServerShutdownCheck());
        }

        void ParseCommandLineArgs()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--port" when i + 1 < args.Length:
                        int.TryParse(args[i + 1], out m_Port);
                        break;
                    case "--room-id" when i + 1 < args.Length:
                        m_RoomId = args[i + 1];
                        break;
                    case "--server-secret" when i + 1 < args.Length:
                        m_ServerSecret = args[i + 1];
                        break;
                    case "--max-players" when i + 1 < args.Length:
                        int.TryParse(args[i + 1], out m_MaxPlayers);
                        break;
                    case "--master-server-url" when i + 1 < args.Length:
                        m_MasterServerUrl = args[i + 1];
                        break;
                }
            }
        }

        bool IsDedicatedServer()
        {
#if UNITY_SERVER
            return true;
#else
            return Environment.GetCommandLineArgs().Contains("--server");
#endif
        }

        IEnumerator HeartbeatLoop()
        {
            // Wait for server to be fully ready
            yield return new WaitForSeconds(5f);

            while (true)
            {
                string status = NetworkServer.active ? "ready" : "starting";
                int playerCount = NetworkServer.connections.Count;
                // Don't count the local connection if there is one (there shouldn't be on dedicated)
                SendHeartbeat(status, playerCount);
                yield return new WaitForSeconds(3f);  // Increased frequency for faster room readiness detection
            }
        }

        IEnumerator EmptyServerShutdownCheck()
        {
            float emptyTime = 0f;
            // Grace period: don't check for the first 30 seconds
            yield return new WaitForSeconds(30f);

            while (true)
            {
                yield return new WaitForSeconds(5f);
                if (NetworkServer.connections.Count == 0)
                {
                    emptyTime += 5f;
                    if (emptyTime > 60f)
                    {
                        Debug.Log("[DedicatedServer] Empty for 60s, shutting down");
                        SendHeartbeat("closing", 0);
                        yield return new WaitForSeconds(1f);
                        Application.Quit();
                        yield break;
                    }
                }
                else
                {
                    emptyTime = 0f;
                }
            }
        }

        void SendHeartbeat(string status, int playerCount)
        {
            if (string.IsNullOrEmpty(m_MasterServerUrl) || string.IsNullOrEmpty(m_RoomId))
                return;

            StartCoroutine(SendHeartbeatCoroutine(status, playerCount));
        }

        IEnumerator SendHeartbeatCoroutine(string status, int playerCount)
        {
            string url = m_MasterServerUrl.TrimEnd('/') + "/api/heartbeat";
            string json = JsonUtility.ToJson(new HeartbeatData
            {
                room_id = m_RoomId,
                server_secret = m_ServerSecret,
                current_players = playerCount,
                status = status
            });

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[DedicatedServer] Heartbeat failed: {request.error}");
            }
        }

        [Serializable]
        struct HeartbeatData
        {
            public string room_id;
            public string server_secret;
            public int current_players;
            public string status;
        }
    }
}
