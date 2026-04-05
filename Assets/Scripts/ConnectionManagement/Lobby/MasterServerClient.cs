using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.BossRoom.ConnectionManagement.Lobby
{
    /// <summary>
    /// HTTP client for communicating with the FastAPI master server.
    /// Uses UnityWebRequest wrapped in Tasks for async/await support.
    /// </summary>
    public class MasterServerClient
    {
        readonly string m_BaseUrl;

        public MasterServerClient(string baseUrl)
        {
            m_BaseUrl = baseUrl.TrimEnd('/');
        }

        public async Task<RoomInfo[]> GetRoomsAsync()
        {
            string json = await GetAsync("/api/rooms");
            // FastAPI returns a JSON array directly, wrap it for JsonUtility
            string wrapped = "{\"items\":" + json + "}";
            Debug.Log($"[MasterServerClient] GetRoomsAsync raw JSON: {json.Substring(0, Math.Min(200, json.Length))}...");
            var response = JsonUtility.FromJson<RoomListResponse>(wrapped);
            if (response == null)
            {
                Debug.LogError($"[MasterServerClient] Failed to parse room list. Wrapped JSON: {wrapped.Substring(0, Math.Min(300, wrapped.Length))}");
                return Array.Empty<RoomInfo>();
            }
            Debug.Log($"[MasterServerClient] Parsed {response.items?.Length ?? 0} rooms");
            return response?.items ?? Array.Empty<RoomInfo>();
        }

        public async Task<JoinResponse> JoinRoomAsync(string roomId, string password, string playerName)
        {
            var body = new JoinRoomRequest
            {
                room_id = roomId,
                password = string.IsNullOrEmpty(password) ? null : password,
                player_name = playerName
            };
            string json = await PostAsync("/api/rooms/join", JsonUtility.ToJson(body));
            return JsonUtility.FromJson<JoinResponse>(json);
        }

        public async Task<RoomStatusResponse> GetRoomStatusAsync(string roomId)
        {
            string json = await GetAsync($"/api/rooms/{roomId}/status");
            return JsonUtility.FromJson<RoomStatusResponse>(json);
        }

        public async Task<bool> SetRoomPrivateAsync(string roomId, string playerName, string password)
        {
            var body = new SetPrivateRequest
            {
                room_id = roomId,
                player_name = playerName,
                password = password
            };
            try
            {
                string json = await PostAsync($"/api/rooms/{roomId}/set-private", JsonUtility.ToJson(body));
                Debug.Log($"[MasterServerClient] SetRoomPrivate response: {json}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MasterServerClient] SetRoomPrivate failed: {e.Message}");
                return false;
            }
        }

        public async Task<bool> StartGameAsync(string roomId, string playerName)
        {
            var body = new StartGameRequest
            {
                room_id = roomId,
                player_name = playerName
            };
            try
            {
                string json = await PostAsync($"/api/rooms/{roomId}/start", JsonUtility.ToJson(body));
                Debug.Log($"[MasterServerClient] StartGame response: {json}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MasterServerClient] StartGame failed: {e.Message}");
                return false;
            }
        }

        async Task<string> GetAsync(string endpoint)
        {
            string url = m_BaseUrl + endpoint;
            using var request = UnityWebRequest.Get(url);
            var op = request.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
                throw new Exception($"GET {url} failed: {request.error}");

            return request.downloadHandler.text;
        }

        async Task<string> PostAsync(string endpoint, string jsonBody)
        {
            string url = m_BaseUrl + endpoint;
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var op = request.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string body = request.downloadHandler?.text ?? "";
                throw new Exception($"POST {url} failed: {request.error} — {body}");
            }

            return request.downloadHandler.text;
        }
    }
}
