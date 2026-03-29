using System;
using System.Collections;
using TMPro;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.ConnectionManagement.Lobby;
using Unity.BossRoom.Gameplay.Configuration;
using Unity.BossRoom.Infrastructure;
using Unity.BossRoom.Utils;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Unity.BossRoom.Gameplay.UI
{
    /// <summary>
    /// UI mediator for the lobby browser. Talks to the FastAPI master server
    /// to list, create, and join rooms, then connects via Mirror.
    /// </summary>
    public class LobbyUIMediator : MonoBehaviour
    {
        [Header("General")]
        [SerializeField] CanvasGroup m_CanvasGroup;
        [SerializeField] TextMeshProUGUI m_PlayerNameLabel;
        [SerializeField] TMP_InputField m_PlayerNameInput;
        [SerializeField] GameObject m_LoadingSpinner;
        [SerializeField] TextMeshProUGUI m_StatusText;

        [Header("Room List")]
        [SerializeField] Transform m_RoomListContent;
        [SerializeField] GameObject m_RoomEntryPrefab;
        [SerializeField] Button m_RefreshButton;
        [SerializeField] TextMeshProUGUI m_NoRoomsText;

        [Header("Create Room")]
        [SerializeField] GameObject m_CreateRoomPanel;
        [SerializeField] TMP_InputField m_RoomNameInput;
        [SerializeField] TMP_InputField m_RoomPasswordInput;
        [SerializeField] TMP_Dropdown m_MaxPlayersDropdown;
        [SerializeField] Button m_CreateRoomButton;
        [SerializeField] Button m_ShowCreatePanelButton;
        [SerializeField] Button m_CancelCreateButton;

        [Header("Join Password")]
        [SerializeField] GameObject m_PasswordPanel;
        [SerializeField] TMP_InputField m_JoinPasswordInput;
        [SerializeField] Button m_ConfirmJoinButton;
        [SerializeField] Button m_CancelPasswordButton;

        [Header("Direct IP")]
        [SerializeField] GameObject m_DirectIPPanel;
        [SerializeField] TMP_InputField m_IPAddressInput;
        [SerializeField] TMP_InputField m_IPPortInput;
        [SerializeField] Button m_ConnectIPButton;
        [SerializeField] Button m_HostIPButton;
        [SerializeField] Button m_ShowDirectIPButton;
        [SerializeField] Button m_CancelDirectIPButton;

        [Header("Connection")]
        [SerializeField] GameObject m_ConnectingPanel;
        [SerializeField] TextMeshProUGUI m_ConnectingText;
        [SerializeField] Button m_CancelConnectingButton;

        [Header("Master Server")]
        [SerializeField] string m_MasterServerUrl = "http://127.0.0.1:8000";
        [SerializeField] float m_AutoRefreshInterval = 5f;

        [Inject] ConnectionManager m_ConnectionManager;
        [Inject] NameGenerationData m_NameGenerationData;

        ISubscriber<ConnectStatus> m_ConnectStatusSubscriber;

        MasterServerClient m_Client;
        RoomInfo[] m_CachedRooms;
        string m_PendingJoinRoomId;
        Coroutine m_AutoRefreshCoroutine;
        Coroutine m_ActiveRefreshCoroutine;
        Coroutine m_WaitForReadyCoroutine;

        [Inject]
        void InjectDependencies(ISubscriber<ConnectStatus> connectStatusSubscriber)
        {
            m_ConnectStatusSubscriber = connectStatusSubscriber;
            m_ConnectStatusSubscriber.Subscribe(OnConnectStatusMessage);
        }

        void Awake()
        {
            Hide();
            m_Client = new MasterServerClient(m_MasterServerUrl);
        }

        void Start()
        {
            // Button listeners
            if (m_RefreshButton != null)
                m_RefreshButton.onClick.AddListener(StartSingleRefresh);

            if (m_CreateRoomButton != null)
                m_CreateRoomButton.onClick.AddListener(() => StartCoroutine(CreateRoomCoroutine()));

            if (m_ShowCreatePanelButton != null)
                m_ShowCreatePanelButton.onClick.AddListener(ShowCreatePanel);

            if (m_CancelCreateButton != null)
                m_CancelCreateButton.onClick.AddListener(HideCreatePanel);

            if (m_ConfirmJoinButton != null)
                m_ConfirmJoinButton.onClick.AddListener(() => StartCoroutine(ConfirmJoinCoroutine()));

            if (m_CancelPasswordButton != null)
                m_CancelPasswordButton.onClick.AddListener(HidePasswordPanel);

            if (m_CancelConnectingButton != null)
                m_CancelConnectingButton.onClick.AddListener(OnCancelConnecting);

            if (m_ShowDirectIPButton != null)
                m_ShowDirectIPButton.onClick.AddListener(ShowDirectIPPanel);

            if (m_CancelDirectIPButton != null)
                m_CancelDirectIPButton.onClick.AddListener(HideDirectIPPanel);

            if (m_ConnectIPButton != null)
                m_ConnectIPButton.onClick.AddListener(OnConnectIPClicked);

            if (m_HostIPButton != null)
                m_HostIPButton.onClick.AddListener(OnHostIPClicked);

            // Initial panel state
            if (m_CreateRoomPanel != null) m_CreateRoomPanel.SetActive(false);
            if (m_PasswordPanel != null) m_PasswordPanel.SetActive(false);
            if (m_ConnectingPanel != null) m_ConnectingPanel.SetActive(false);
            if (m_DirectIPPanel != null) m_DirectIPPanel.SetActive(false);
            if (m_LoadingSpinner != null) m_LoadingSpinner.SetActive(false);

            RegenerateName();
        }

        void OnDestroy()
        {
            m_ConnectStatusSubscriber?.Unsubscribe(OnConnectStatusMessage);
        }

        void OnConnectStatusMessage(ConnectStatus status)
        {
            if (m_LoadingSpinner != null) m_LoadingSpinner.SetActive(false);
            if (m_ConnectingPanel != null) m_ConnectingPanel.SetActive(false);

            if (status != ConnectStatus.Success)
            {
                SetStatus($"Connection failed: {status}");
                StartAutoRefresh();
            }
        }

        // ===========================================================
        // Public API (called from ClientMainMenuState / buttons)
        // ===========================================================

        public void Show()
        {
            if (m_CanvasGroup == null) return;

            m_CanvasGroup.alpha = 1f;
            m_CanvasGroup.interactable = true;
            m_CanvasGroup.blocksRaycasts = true;

            if (m_LoadingSpinner != null) m_LoadingSpinner.SetActive(false);
            if (m_ConnectingPanel != null) m_ConnectingPanel.SetActive(false);
            if (m_PasswordPanel != null) m_PasswordPanel.SetActive(false);
            SetStatus("");

            StartSingleRefresh();
            StartAutoRefresh();
        }

        public void Hide()
        {
            if (m_CanvasGroup == null) return;

            m_CanvasGroup.alpha = 0f;
            m_CanvasGroup.interactable = false;
            m_CanvasGroup.blocksRaycasts = false;

            StopAutoRefresh();
        }

        public void RegenerateName()
        {
            if (m_NameGenerationData == null) return;

            string generated = m_NameGenerationData.GenerateName();
            if (m_PlayerNameLabel != null) m_PlayerNameLabel.text = generated;
            if (m_PlayerNameInput != null) m_PlayerNameInput.text = generated;
        }

        public string GetPlayerName()
        {
            // Prefer input field if present, fall back to label
            if (m_PlayerNameInput != null && !string.IsNullOrWhiteSpace(m_PlayerNameInput.text))
                return m_PlayerNameInput.text.Trim();
            if (m_PlayerNameLabel != null && !string.IsNullOrWhiteSpace(m_PlayerNameLabel.text))
                return m_PlayerNameLabel.text.Trim();
            return "Player";
        }

        public void HostAndPlay()
        {
            Hide();
            StopAutoRefresh();

            if (m_ConnectingPanel != null) m_ConnectingPanel.SetActive(true);
            if (m_ConnectingText != null) m_ConnectingText.text = "Starting host...";

            string playerName = GetPlayerName();

            var netManager = BossRoomNetworkManager.singleton;
            if (netManager != null)
            {
                netManager.PendingClientPayload = new ConnectionPayload
                {
                    playerId = ClientPrefs.GetGuid(),
                    playerName = playerName
                };
            }

            // m_ConnectionManager may be null if VContainer hasn't injected it yet
            var connMgr = m_ConnectionManager;
            if (connMgr == null)
                connMgr = FindObjectOfType<ConnectionManager>();

            if (connMgr != null)
            {
                Debug.Log($"[Lobby] HostAndPlay: starting host as '{playerName}'");
                connMgr.StartHostIp(playerName, "127.0.0.1", 7777);
            }
            else
            {
                Debug.LogError("[Lobby] HostAndPlay: ConnectionManager not found!");
            }
        }

        public void OnCancelConnecting()
        {
            if (m_WaitForReadyCoroutine != null)
            {
                StopCoroutine(m_WaitForReadyCoroutine);
                m_WaitForReadyCoroutine = null;
            }
            if (m_ConnectingPanel != null) m_ConnectingPanel.SetActive(false);
            if (m_LoadingSpinner != null) m_LoadingSpinner.SetActive(false);
            if (m_ConnectionManager != null) m_ConnectionManager.RequestShutdown();
            StartAutoRefresh();
        }

        // ===========================================================
        // Auto Refresh
        // ===========================================================

        void StartAutoRefresh()
        {
            StopAutoRefresh();
            if (m_AutoRefreshInterval > 0)
                m_AutoRefreshCoroutine = StartCoroutine(AutoRefreshLoop());
        }

        void StopAutoRefresh()
        {
            if (m_AutoRefreshCoroutine != null)
            {
                StopCoroutine(m_AutoRefreshCoroutine);
                m_AutoRefreshCoroutine = null;
            }
        }

        IEnumerator AutoRefreshLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(m_AutoRefreshInterval);
                yield return StartSingleRefreshCoroutine();
            }
        }

        IEnumerator StartSingleRefreshCoroutine()
        {
            if (m_ActiveRefreshCoroutine != null)
                StopCoroutine(m_ActiveRefreshCoroutine);
            m_ActiveRefreshCoroutine = StartCoroutine(RefreshRoomsCoroutine());
            yield return m_ActiveRefreshCoroutine;
        }

        // ===========================================================
        // Room List
        // ===========================================================

        void StartSingleRefresh()
        {
            if (m_ActiveRefreshCoroutine != null)
                StopCoroutine(m_ActiveRefreshCoroutine);
            m_ActiveRefreshCoroutine = StartCoroutine(RefreshRoomsCoroutine());
        }

        IEnumerator RefreshRoomsCoroutine()
        {
            if (m_LoadingSpinner != null) m_LoadingSpinner.SetActive(true);
            SetStatus("");

            var task = m_Client.GetRoomsAsync();
            while (!task.IsCompleted)
                yield return null;

            if (m_LoadingSpinner != null) m_LoadingSpinner.SetActive(false);

            if (task.IsFaulted)
            {
                SetStatus("Error connecting to master server");
                Debug.LogError($"[Lobby] Refresh failed: {task.Exception?.InnerException?.Message}");
                yield break;
            }

            m_CachedRooms = task.Result;
            PopulateRoomList();
        }

        void PopulateRoomList()
        {
            if (m_RoomListContent == null) return;

            // Clear existing entries — detach before Destroy so the LayoutGroup
            // stops counting them immediately (Destroy itself is deferred).
            for (int i = m_RoomListContent.childCount - 1; i >= 0; i--)
            {
                var go = m_RoomListContent.GetChild(i).gameObject;
                go.transform.SetParent(null);
                Destroy(go);
            }

            if (m_CachedRooms == null || m_CachedRooms.Length == 0)
            {
                if (m_NoRoomsText != null) m_NoRoomsText.gameObject.SetActive(true);
                return;
            }

            if (m_NoRoomsText != null) m_NoRoomsText.gameObject.SetActive(false);

            if (m_RoomEntryPrefab == null)
            {
                Debug.LogWarning("[Lobby] RoomEntryPrefab is not assigned");
                return;
            }

            foreach (var room in m_CachedRooms)
            {
                var entry = Instantiate(m_RoomEntryPrefab, m_RoomListContent);
                var roomEntry = entry.GetComponent<LobbyRoomEntry>();
                if (roomEntry != null)
                {
                    roomEntry.Setup(room, OnJoinRoomClicked);
                }
            }
        }

        // ===========================================================
        // Join Room
        // ===========================================================

        void OnJoinRoomClicked(RoomInfo room)
        {
            if (room.has_password)
            {
                m_PendingJoinRoomId = room.room_id;
                if (m_JoinPasswordInput != null) m_JoinPasswordInput.text = "";
                if (m_PasswordPanel != null) m_PasswordPanel.SetActive(true);
            }
            else
            {
                StartCoroutine(JoinRoomCoroutine(room.room_id, null));
            }
        }

        void HidePasswordPanel()
        {
            if (m_PasswordPanel != null) m_PasswordPanel.SetActive(false);
        }

        IEnumerator ConfirmJoinCoroutine()
        {
            HidePasswordPanel();
            string password = m_JoinPasswordInput != null ? m_JoinPasswordInput.text : "";
            yield return JoinRoomCoroutine(m_PendingJoinRoomId, password);
        }

        IEnumerator JoinRoomCoroutine(string roomId, string password)
        {
            if (m_LoadingSpinner != null) m_LoadingSpinner.SetActive(true);
            SetStatus("Joining room...");

            string playerName = GetPlayerName();
            var task = m_Client.JoinRoomAsync(roomId, password, playerName);
            while (!task.IsCompleted)
                yield return null;

            if (m_LoadingSpinner != null) m_LoadingSpinner.SetActive(false);

            if (task.IsFaulted)
            {
                SetStatus("Error joining room");
                Debug.LogError($"[Lobby] Join failed: {task.Exception?.InnerException?.Message}");
                yield break;
            }

            var response = task.Result;
            if (!response.success)
            {
                SetStatus(response.error ?? "Failed to join");
                yield break;
            }

            // Connect via Mirror
            ConnectToGameServer(response.host_address, response.port, playerName, response.room_key);
        }

        // ===========================================================
        // Create Room
        // ===========================================================

        void ShowCreatePanel()
        {
            if (m_CreateRoomPanel != null) m_CreateRoomPanel.SetActive(true);
        }

        void HideCreatePanel()
        {
            if (m_CreateRoomPanel != null) m_CreateRoomPanel.SetActive(false);
        }

        IEnumerator CreateRoomCoroutine()
        {
            string roomName = m_RoomNameInput != null ? m_RoomNameInput.text : "";
            if (string.IsNullOrWhiteSpace(roomName))
            {
                SetStatus("Room name is required");
                yield break;
            }

            string password = m_RoomPasswordInput != null ? m_RoomPasswordInput.text : null;
            if (string.IsNullOrWhiteSpace(password)) password = null;

            // Dropdown: index 0 = 2 players, 1 = 4, 2 = 8
            int maxPlayers = 8;
            if (m_MaxPlayersDropdown != null)
            {
                maxPlayers = m_MaxPlayersDropdown.value switch
                {
                    0 => 2,
                    1 => 4,
                    2 => 8,
                    _ => 8
                };
            }

            if (m_LoadingSpinner != null) m_LoadingSpinner.SetActive(true);
            SetStatus("Creating room...");
            StopAutoRefresh(); // pause auto-refresh while waiting

            string playerName = GetPlayerName();
            var task = m_Client.CreateRoomAsync(roomName, password, maxPlayers, playerName);
            while (!task.IsCompleted)
                yield return null;

            if (m_LoadingSpinner != null) m_LoadingSpinner.SetActive(false);

            if (task.IsFaulted)
            {
                SetStatus("Error creating room");
                Debug.LogError($"[Lobby] Create failed: {task.Exception?.InnerException?.Message}");
                StartAutoRefresh();
                yield break;
            }

            var response = task.Result;
            if (response == null)
            {
                SetStatus("Error parsing room response");
                Debug.LogError("[Lobby] Create response was null");
                StartAutoRefresh();
                yield break;
            }

            HideCreatePanel();
            Debug.Log($"[Lobby] Room created: {response.room_id}, waiting for server ready...");

            // Wait for the dedicated server to become ready before connecting
            m_WaitForReadyCoroutine = StartCoroutine(WaitForServerReadyThenConnect(
                response.room_id, response.host_address, response.port, playerName, response.room_key));
            yield return m_WaitForReadyCoroutine;
            m_WaitForReadyCoroutine = null;
        }

        IEnumerator WaitForServerReadyThenConnect(string roomId, string hostAddress, int port, string playerName, string roomKey)
        {
            if (m_ConnectingPanel != null) m_ConnectingPanel.SetActive(true);
            if (m_LoadingSpinner != null) m_LoadingSpinner.SetActive(true);

            float waited = 0f;
            const float pollInterval = 1.5f;
            const float timeout = 45f;

            while (waited < timeout)
            {
                string msg = $"Waiting for server... ({(int)waited}s)";
                SetStatus(msg);
                if (m_ConnectingText != null) m_ConnectingText.text = msg;

                yield return new WaitForSeconds(pollInterval);
                waited += pollInterval;

                var task = m_Client.GetRoomStatusAsync(roomId);
                while (!task.IsCompleted)
                    yield return null;

                if (task.IsFaulted)
                {
                    Debug.LogWarning($"[Lobby] Error polling room status: {task.Exception?.InnerException?.Message}");
                    continue;
                }

                if (task.Result != null && task.Result.status == "ready")
                {
                    if (m_LoadingSpinner != null) m_LoadingSpinner.SetActive(false);
                    Debug.Log($"[Lobby] Server ready for room {roomId}, connecting on port {port}...");
                    ConnectToGameServer(hostAddress, port, playerName, roomKey);
                    yield break;
                }
            }

            // Timeout — server never became ready
            if (m_LoadingSpinner != null) m_LoadingSpinner.SetActive(false);
            if (m_ConnectingPanel != null) m_ConnectingPanel.SetActive(false);
            SetStatus("Server failed to start (timeout)");
            Debug.LogError($"[Lobby] Server for room {roomId} did not become ready within {timeout}s");
            StartAutoRefresh();
        }

        // ===========================================================
        // Connect via Mirror
        // ===========================================================

        void ConnectToGameServer(string address, int port, string playerName, string roomKey)
        {
            StopAutoRefresh();

            if (m_ConnectingPanel != null) m_ConnectingPanel.SetActive(true);
            if (m_ConnectingText != null) m_ConnectingText.text = $"Connecting to {address}:{port}...";
            if (m_LoadingSpinner != null) m_LoadingSpinner.SetActive(true);

            // Store room key in the pending payload so it's sent to the game server
            var netManager = BossRoomNetworkManager.singleton;
            if (netManager != null)
            {
                netManager.PendingClientPayload = new ConnectionPayload
                {
                    playerId = ClientPrefs.GetGuid(),
                    playerName = playerName,
                    roomKey = roomKey
                };
            }

            if (m_ConnectionManager != null)
                m_ConnectionManager.StartClientIp(playerName, address, port);
        }

        // ===========================================================
        // Direct IP Connect / Host
        // ===========================================================

        void ShowDirectIPPanel()
        {
            if (m_DirectIPPanel != null) m_DirectIPPanel.SetActive(true);
        }

        void HideDirectIPPanel()
        {
            if (m_DirectIPPanel != null) m_DirectIPPanel.SetActive(false);
        }

        void OnConnectIPClicked()
        {
            string ip = m_IPAddressInput != null ? m_IPAddressInput.text.Trim() : "127.0.0.1";
            if (string.IsNullOrWhiteSpace(ip)) ip = "127.0.0.1";

            int port = 7777;
            if (m_IPPortInput != null && !string.IsNullOrWhiteSpace(m_IPPortInput.text))
                int.TryParse(m_IPPortInput.text.Trim(), out port);

            HideDirectIPPanel();
            ConnectToGameServer(ip, port, GetPlayerName(), null);
        }

        void OnHostIPClicked()
        {
            string ip = m_IPAddressInput != null ? m_IPAddressInput.text.Trim() : "127.0.0.1";
            if (string.IsNullOrWhiteSpace(ip)) ip = "127.0.0.1";

            int port = 7777;
            if (m_IPPortInput != null && !string.IsNullOrWhiteSpace(m_IPPortInput.text))
                int.TryParse(m_IPPortInput.text.Trim(), out port);

            HideDirectIPPanel();
            StopAutoRefresh();

            if (m_ConnectingPanel != null) m_ConnectingPanel.SetActive(true);
            if (m_ConnectingText != null) m_ConnectingText.text = $"Starting host on {ip}:{port}...";

            if (m_ConnectionManager != null)
                m_ConnectionManager.StartHostIp(GetPlayerName(), ip, port);
        }

        // ===========================================================
        // Helpers
        // ===========================================================

        void SetStatus(string message)
        {
            if (m_StatusText != null)
                m_StatusText.text = message;
        }
    }
}
