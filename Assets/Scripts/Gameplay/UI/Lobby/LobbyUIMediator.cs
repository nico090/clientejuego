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
    /// to list pre-created rooms and join them. The first player to join
    /// becomes admin (player 0) and can make the room private or start the game.
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

        [Header("Join Password")]
        [SerializeField] GameObject m_PasswordPanel;
        [SerializeField] TMP_InputField m_JoinPasswordInput;
        [SerializeField] Button m_ConfirmJoinButton;
        [SerializeField] Button m_CancelPasswordButton;

        [Header("Admin Controls")]
        [SerializeField] GameObject m_AdminPanel;
        [SerializeField] TMP_InputField m_AdminPasswordInput;
        [SerializeField] Button m_SetPrivateButton;
        [SerializeField] Button m_SetPublicButton;
        [SerializeField] Button m_StartGameButton;
        [SerializeField] TextMeshProUGUI m_AdminStatusText;
        [SerializeField] TextMeshProUGUI m_AdminRoomNameText;
        [SerializeField] TextMeshProUGUI m_AdminPlayerCountText;

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
        Coroutine m_AdminPollCoroutine;

        // Admin state — set when the player joins and is_admin == true
        bool m_IsAdmin;
        string m_AdminRoomId;

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

            // Admin panel buttons
            if (m_SetPrivateButton != null)
                m_SetPrivateButton.onClick.AddListener(() => StartCoroutine(SetPrivateCoroutine()));

            if (m_SetPublicButton != null)
                m_SetPublicButton.onClick.AddListener(() => StartCoroutine(SetPublicCoroutine()));

            if (m_StartGameButton != null)
                m_StartGameButton.onClick.AddListener(() => StartCoroutine(StartGameCoroutine()));

            // Initial panel state
            if (m_PasswordPanel != null) m_PasswordPanel.SetActive(false);
            if (m_ConnectingPanel != null) m_ConnectingPanel.SetActive(false);
            if (m_DirectIPPanel != null) m_DirectIPPanel.SetActive(false);
            if (m_AdminPanel != null) m_AdminPanel.SetActive(false);
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
                HideAdminPanel();
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
            if (m_AdminPanel != null) m_AdminPanel.SetActive(false);
            SetStatus("");

            m_IsAdmin = false;
            m_AdminRoomId = null;

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
            StopAdminPoll();
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
            if (m_PlayerNameInput != null && !string.IsNullOrWhiteSpace(m_PlayerNameInput.text))
                return m_PlayerNameInput.text.Trim();
            if (m_PlayerNameLabel != null && !string.IsNullOrWhiteSpace(m_PlayerNameLabel.text))
                return m_PlayerNameLabel.text.Trim();
            return "Player";
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
            HideAdminPanel();
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
            if (room.has_password || room.is_locked)
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

            // If this player is admin, show the admin panel instead of connecting immediately
            if (response.is_admin)
            {
                m_IsAdmin = true;
                m_AdminRoomId = roomId;
                Debug.Log($"[Lobby] Player '{playerName}' is ADMIN of room {roomId}");
                ShowAdminPanel(roomId, playerName, response);
                yield break;
            }

            // Non-admin: connect to the game server directly
            ConnectToGameServer(response.host_address, response.port, playerName, response.room_key);
        }

        // ===========================================================
        // Admin Panel
        // ===========================================================

        void ShowAdminPanel(string roomId, string playerName, JoinResponse joinResponse)
        {
            StopAutoRefresh();

            if (m_AdminPanel != null) m_AdminPanel.SetActive(true);

            // Find the room info for display
            RoomInfo roomInfo = null;
            if (m_CachedRooms != null)
            {
                foreach (var r in m_CachedRooms)
                {
                    if (r.room_id == roomId) { roomInfo = r; break; }
                }
            }

            if (m_AdminRoomNameText != null)
                m_AdminRoomNameText.text = roomInfo != null ? roomInfo.name : roomId;

            UpdateAdminStatus("Waiting for players...");

            // Store join response so we can connect later when admin starts the game
            m_PendingAdminJoinResponse = joinResponse;

            // Start polling room status to show player count
            StartAdminPoll(roomId);
        }

        JoinResponse m_PendingAdminJoinResponse;

        void HideAdminPanel()
        {
            m_IsAdmin = false;
            m_AdminRoomId = null;
            m_PendingAdminJoinResponse = null;
            StopAdminPoll();
            if (m_AdminPanel != null) m_AdminPanel.SetActive(false);
        }

        void UpdateAdminStatus(string msg)
        {
            if (m_AdminStatusText != null) m_AdminStatusText.text = msg;
        }

        void StartAdminPoll(string roomId)
        {
            StopAdminPoll();
            m_AdminPollCoroutine = StartCoroutine(AdminPollLoop(roomId));
        }

        void StopAdminPoll()
        {
            if (m_AdminPollCoroutine != null)
            {
                StopCoroutine(m_AdminPollCoroutine);
                m_AdminPollCoroutine = null;
            }
        }

        IEnumerator AdminPollLoop(string roomId)
        {
            while (true)
            {
                var task = m_Client.GetRoomStatusAsync(roomId);
                while (!task.IsCompleted)
                    yield return null;

                if (!task.IsFaulted && task.Result != null)
                {
                    if (m_AdminPlayerCountText != null)
                        m_AdminPlayerCountText.text = $"Players: {task.Result.current_players}";
                }

                yield return new WaitForSeconds(2f);
            }
        }

        IEnumerator SetPrivateCoroutine()
        {
            if (!m_IsAdmin || string.IsNullOrEmpty(m_AdminRoomId)) yield break;

            string password = m_AdminPasswordInput != null ? m_AdminPasswordInput.text : "";
            if (string.IsNullOrWhiteSpace(password))
            {
                UpdateAdminStatus("Enter a password first");
                yield break;
            }

            UpdateAdminStatus("Setting room to private...");
            string playerName = GetPlayerName();
            var task = m_Client.SetRoomPrivateAsync(m_AdminRoomId, playerName, password);
            while (!task.IsCompleted)
                yield return null;

            if (task.Result)
                UpdateAdminStatus("Room is now PRIVATE");
            else
                UpdateAdminStatus("Failed to set private");
        }

        IEnumerator SetPublicCoroutine()
        {
            if (!m_IsAdmin || string.IsNullOrEmpty(m_AdminRoomId)) yield break;

            UpdateAdminStatus("Setting room to public...");
            string playerName = GetPlayerName();
            var task = m_Client.SetRoomPrivateAsync(m_AdminRoomId, playerName, null);
            while (!task.IsCompleted)
                yield return null;

            if (task.Result)
                UpdateAdminStatus("Room is now PUBLIC");
            else
                UpdateAdminStatus("Failed to set public");
        }

        IEnumerator StartGameCoroutine()
        {
            if (!m_IsAdmin || string.IsNullOrEmpty(m_AdminRoomId)) yield break;

            UpdateAdminStatus("Starting game...");
            string playerName = GetPlayerName();
            var task = m_Client.StartGameAsync(m_AdminRoomId, playerName);
            while (!task.IsCompleted)
                yield return null;

            if (!task.Result)
            {
                UpdateAdminStatus("Failed to start game");
                yield break;
            }

            // Game started — now connect the admin to the game server
            if (m_PendingAdminJoinResponse != null)
            {
                StopAdminPoll();
                if (m_AdminPanel != null) m_AdminPanel.SetActive(false);
                ConnectToGameServer(
                    m_PendingAdminJoinResponse.host_address,
                    m_PendingAdminJoinResponse.port,
                    playerName,
                    m_PendingAdminJoinResponse.room_key
                );
            }
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
            if (m_WaitForReadyCoroutine != null)
            {
                StopCoroutine(m_WaitForReadyCoroutine);
                m_WaitForReadyCoroutine = null;
            }

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
            if (m_WaitForReadyCoroutine != null)
            {
                StopCoroutine(m_WaitForReadyCoroutine);
                m_WaitForReadyCoroutine = null;
            }

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
