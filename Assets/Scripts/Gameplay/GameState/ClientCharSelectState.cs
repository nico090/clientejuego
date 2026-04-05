using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.UI;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using VContainer;
using Avatar = Unity.BossRoom.Gameplay.Configuration.Avatar;

namespace Unity.BossRoom.Gameplay.GameState
{
    /// <summary>
    /// Client specialization of the Character Select game state. Mainly controls the UI during character-select.
    /// </summary>
    [RequireComponent(typeof(NetworkHooks))]
    public class ClientCharSelectState : GameStateBehaviour
    {
        /// <summary>
        /// Reference to the scene's state object so that UI can access state.
        /// </summary>
        public static ClientCharSelectState Instance { get; private set; }

        [FormerlySerializedAs("m_NetcodeHooks")]
        [SerializeField]
        NetworkHooks m_NetworkHooks;

        public override GameState ActiveState
        {
            get { return GameState.CharSelect; }
        }

        [SerializeField]
        NetworkCharSelection m_NetworkCharSelection;

        [SerializeField]
        [Tooltip("This is triggered when the player chooses a character")]
        string m_AnimationTriggerOnCharSelect = "BeginRevive";

        [SerializeField]
        [Tooltip("This is triggered when the player presses the \"Ready\" button")]
        string m_AnimationTriggerOnCharChosen = "BeginRevive";

        [Header("Session Seats")]
        [SerializeField]
        [Tooltip("Collection of 8 portrait-boxes, one for each potential session member")]
        List<UICharSelectPlayerSeat> m_PlayerSeats;

        [System.Serializable]
        public class ColorAndIndicator
        {
            public Sprite Indicator;
            public Color Color;
        }

        [Tooltip("Representational information for each player")]
        public ColorAndIndicator[] m_IdentifiersForEachPlayerNumber;

        [SerializeField]
        [Tooltip("Text element containing player count which updates as players connect")]
        TextMeshProUGUI m_NumPlayersText;

        [SerializeField]
        [Tooltip("Text element for the Ready button")]
        TextMeshProUGUI m_ReadyButtonText;

        [Header("UI Elements for different session modes")]
        [SerializeField]
        [Tooltip("UI elements to turn on when the player hasn't chosen their seat yet. Turned off otherwise!")]
        List<GameObject> m_UIElementsForNoSeatChosen;

        [SerializeField]
        [Tooltip("UI elements to turn on when the player has locked in their seat choice (and is now waiting for other players to do the same). Turned off otherwise!")]
        List<GameObject> m_UIElementsForSeatChosen;

        [FormerlySerializedAs("m_UIElementsForLobbyEnding")]
        [SerializeField]
        [Tooltip("UI elements to turn on when the session is closed (and game is about to start). Turned off otherwise!")]
        List<GameObject> m_UIElementsForSessionEnding;

        [SerializeField]
        [Tooltip("UI elements to turn on when there's been a fatal error (and the client cannot proceed). Turned off otherwise!")]
        List<GameObject> m_UIElementsForFatalError;

        [Header("Misc")]
        [SerializeField]
        [Tooltip("The controller for the class-info box")]
        UICharSelectClassInfoBox m_ClassInfoBox;

        [SerializeField]
        Transform m_CharacterGraphicsParent;

        int m_LastSeatSelected = -1;
        bool m_HasLocalPlayerLockedIn = false;

        GameObject m_CurrentCharacterGraphics;

        Animator m_CurrentCharacterGraphicsAnimator;

        Dictionary<Guid, GameObject> m_SpawnedCharacterGraphics = new Dictionary<Guid, GameObject>();

        enum SessionMode
        {
            ChooseSeat,
            SeatChosen,
            SessionEnding,
            FatalError,
        }

        Dictionary<SessionMode, List<GameObject>> m_SessionUIElementsByMode;

        // Match result banner
        GameObject m_MatchResultBanner;
        const float k_BannerDisplayDuration = 8f;

        // Name input (created at runtime)
        TMP_InputField m_NameInputField;
        bool m_NameConfirmed;

        [Inject]
        ConnectionManager m_ConnectionManager;

        protected override void Awake()
        {
            base.Awake();
            Instance = this;

            if (m_NetworkHooks == null)
            {
                m_NetworkHooks = GetComponent<NetworkHooks>();
            }

            m_NetworkHooks.OnNetworkSpawn += OnNetworkSpawn;
            m_NetworkHooks.OnNetworkDespawn += OnNetworkDespawn;

            m_SessionUIElementsByMode = new Dictionary<SessionMode, List<GameObject>>()
            {
                { SessionMode.ChooseSeat, m_UIElementsForNoSeatChosen },
                { SessionMode.SeatChosen, m_UIElementsForSeatChosen },
                { SessionMode.SessionEnding, m_UIElementsForSessionEnding },
                { SessionMode.FatalError, m_UIElementsForFatalError },
            };
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            base.OnDestroy();
        }

        protected override void Start()
        {
            base.Start();
            for (int i = 0; i < m_PlayerSeats.Count; ++i)
            {
                m_PlayerSeats[i].Initialize(i);
            }

            ConfigureUIForSessionMode(SessionMode.ChooseSeat);
            UpdateCharacterSelection(NetworkCharSelection.SeatState.Inactive);

            CreateNameInputUI();
        }

        void OnNetworkDespawn()
        {
            if (m_NetworkCharSelection)
            {
                m_NetworkCharSelection.IsSessionClosedChanged -= OnSessionClosedChanged;
                m_NetworkCharSelection.sessionPlayers.Callback -= OnSessionPlayerStateChanged;
                m_NetworkCharSelection.MatchResultChanged -= OnMatchResultChanged;
            }
        }

        void OnNetworkSpawn()
        {
            if (!NetworkClient.active)
            {
                enabled = false;
            }
            else
            {
                m_NetworkCharSelection.IsSessionClosedChanged += OnSessionClosedChanged;
                m_NetworkCharSelection.sessionPlayers.Callback += OnSessionPlayerStateChanged;
                m_NetworkCharSelection.MatchResultChanged += OnMatchResultChanged;

                // Check if match result was already set before we subscribed
                if (!string.IsNullOrEmpty(m_NetworkCharSelection.MatchResultJson))
                {
                    OnMatchResultChanged(m_NetworkCharSelection.MatchResultJson);
                }
            }
        }

        void OnAssignedPlayerNumber(int playerNum)
        {
            m_ClassInfoBox.OnSetPlayerNumber(playerNum);
        }

        void UpdatePlayerCount()
        {
            int count = m_NetworkCharSelection.sessionPlayers.Count;
            var pstr = (count > 1) ? "players" : "player";
            m_NumPlayersText.text = "<b>" + count + "</b> " + pstr + " connected";
        }

        /// <summary>
        /// Called by the server when any of the seats in the session have changed.
        /// </summary>
        void OnSessionPlayerStateChanged(SyncList<NetworkCharSelection.SessionPlayerState>.Operation op, int index,
            NetworkCharSelection.SessionPlayerState oldItem, NetworkCharSelection.SessionPlayerState newItem)
        {
            UpdateSeats();
            UpdatePlayerCount();

            ulong localClientId = NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent(out PersistentPlayer pp) ? pp.OwnerConnectionId : 0ul;
            int localPlayerIdx = -1;
            for (int i = 0; i < m_NetworkCharSelection.sessionPlayers.Count; ++i)
            {
                if (m_NetworkCharSelection.sessionPlayers[i].ClientId == localClientId)
                {
                    localPlayerIdx = i;
                    break;
                }
            }

            if (localPlayerIdx == -1)
            {
                UpdateCharacterSelection(NetworkCharSelection.SeatState.Inactive);
            }
            else if (m_NetworkCharSelection.sessionPlayers[localPlayerIdx].SeatState == NetworkCharSelection.SeatState.Inactive)
            {
                UpdateCharacterSelection(NetworkCharSelection.SeatState.Inactive);
                OnAssignedPlayerNumber(m_NetworkCharSelection.sessionPlayers[localPlayerIdx].PlayerNumber);
            }
            else
            {
                UpdateCharacterSelection(m_NetworkCharSelection.sessionPlayers[localPlayerIdx].SeatState, m_NetworkCharSelection.sessionPlayers[localPlayerIdx].SeatIdx);
            }
        }

        void UpdateCharacterSelection(NetworkCharSelection.SeatState state, int seatIdx = -1)
        {
            bool isNewSeat = m_LastSeatSelected != seatIdx;

            m_LastSeatSelected = seatIdx;
            if (state == NetworkCharSelection.SeatState.Inactive)
            {
                if (m_CurrentCharacterGraphics)
                {
                    m_CurrentCharacterGraphics.SetActive(false);
                }

                m_ClassInfoBox.ConfigureForNoSelection();
            }
            else
            {
                if (seatIdx != -1)
                {
                    if (isNewSeat)
                    {
                        var selectedCharacterGraphics = GetCharacterGraphics(m_NetworkCharSelection.AvatarConfiguration[seatIdx]);

                        if (m_CurrentCharacterGraphics)
                        {
                            m_CurrentCharacterGraphics.SetActive(false);
                        }

                        selectedCharacterGraphics.SetActive(true);
                        m_CurrentCharacterGraphics = selectedCharacterGraphics;
                        m_CurrentCharacterGraphicsAnimator = m_CurrentCharacterGraphics.GetComponent<Animator>();

                        m_ClassInfoBox.ConfigureForClass(m_NetworkCharSelection.AvatarConfiguration[seatIdx].CharacterClass);
                    }
                }

                if (state == NetworkCharSelection.SeatState.LockedIn && !m_HasLocalPlayerLockedIn)
                {
                    m_CurrentCharacterGraphicsAnimator.SetTrigger(m_AnimationTriggerOnCharChosen);
                    ConfigureUIForSessionMode(m_NetworkCharSelection.IsSessionClosed ? SessionMode.SessionEnding : SessionMode.SeatChosen);
                    m_HasLocalPlayerLockedIn = true;
                }
                else if (m_HasLocalPlayerLockedIn && state == NetworkCharSelection.SeatState.Active)
                {
                    if (m_HasLocalPlayerLockedIn)
                    {
                        ConfigureUIForSessionMode(SessionMode.ChooseSeat);
                        m_ClassInfoBox.SetLockedIn(false);
                        m_HasLocalPlayerLockedIn = false;
                    }
                }
                else if (state == NetworkCharSelection.SeatState.Active && isNewSeat)
                {
                    m_CurrentCharacterGraphicsAnimator.SetTrigger(m_AnimationTriggerOnCharSelect);
                }
            }
        }

        void UpdateSeats()
        {
            NetworkCharSelection.SessionPlayerState[] curSeats = new NetworkCharSelection.SessionPlayerState[m_PlayerSeats.Count];
            foreach (NetworkCharSelection.SessionPlayerState playerState in m_NetworkCharSelection.sessionPlayers)
            {
                if (playerState.SeatIdx == -1 || playerState.SeatState == NetworkCharSelection.SeatState.Inactive)
                    continue;
                if (curSeats[playerState.SeatIdx].SeatState == NetworkCharSelection.SeatState.Inactive
                    || (curSeats[playerState.SeatIdx].SeatState == NetworkCharSelection.SeatState.Active && curSeats[playerState.SeatIdx].LastChangeTime < playerState.LastChangeTime))
                {
                    curSeats[playerState.SeatIdx] = playerState;
                }
            }

            for (int i = 0; i < m_PlayerSeats.Count; ++i)
            {
                m_PlayerSeats[i].SetState(curSeats[i].SeatState, curSeats[i].PlayerNumber, curSeats[i].PlayerName);
            }
        }

        void OnSessionClosedChanged(bool wasSessionClosed, bool isSessionClosed)
        {
            if (isSessionClosed)
            {
                ConfigureUIForSessionMode(SessionMode.SessionEnding);
            }
            else
            {
                if (m_LastSeatSelected == -1)
                {
                    ConfigureUIForSessionMode(SessionMode.ChooseSeat);
                }
                else
                {
                    ConfigureUIForSessionMode(SessionMode.SeatChosen);
                    m_ClassInfoBox.ConfigureForClass(m_NetworkCharSelection.AvatarConfiguration[m_LastSeatSelected].CharacterClass);
                }
            }
        }

        void ConfigureUIForSessionMode(SessionMode mode)
        {
            foreach (var list in m_SessionUIElementsByMode.Values)
            {
                foreach (var uiElement in list)
                {
                    uiElement.SetActive(false);
                }
            }

            foreach (var uiElement in m_SessionUIElementsByMode[mode])
            {
                uiElement.SetActive(true);
            }

            bool isSeatsDisabledInThisMode = false;
            switch (mode)
            {
                case SessionMode.ChooseSeat:
                    if (m_LastSeatSelected == -1)
                    {
                        if (m_CurrentCharacterGraphics)
                        {
                            m_CurrentCharacterGraphics.gameObject.SetActive(false);
                        }

                        m_ClassInfoBox.ConfigureForNoSelection();
                    }

                    m_ReadyButtonText.text = "READY!";
                    break;
                case SessionMode.SeatChosen:
                    isSeatsDisabledInThisMode = true;
                    m_ClassInfoBox.SetLockedIn(true);
                    m_ReadyButtonText.text = "UNREADY";
                    break;
                case SessionMode.FatalError:
                    isSeatsDisabledInThisMode = true;
                    m_ClassInfoBox.ConfigureForNoSelection();
                    break;
                case SessionMode.SessionEnding:
                    isSeatsDisabledInThisMode = true;
                    m_ClassInfoBox.ConfigureForNoSelection();
                    break;
            }

            foreach (var seat in m_PlayerSeats)
            {
                seat.SetDisableInteraction(seat.IsLocked() || isSeatsDisabledInThisMode);
            }
        }

        /// <summary>Called directly by UI elements!</summary>
        public void OnPlayerClickedSeat(int seatIdx)
        {
            if (NetworkClient.active && m_NetworkCharSelection != null)
            {
                m_NetworkCharSelection.CmdChangeSeat(seatIdx, false);
            }
        }

        /// <summary>Called directly by UI elements!</summary>
        public void OnPlayerClickedReady()
        {
            if (NetworkClient.active && m_NetworkCharSelection != null)
            {
                m_NetworkCharSelection.CmdChangeSeat(m_LastSeatSelected, !m_HasLocalPlayerLockedIn);
            }
        }

        void CreateNameInputUI()
        {
            // Create an overlay canvas for the name input at the top of the screen
            var canvasGO = new GameObject("NameInputCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // Background panel
            var panelGO = new GameObject("NamePanel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0, 0, 0, 0.6f);

            var panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.3f, 0.88f);
            panelRect.anchorMax = new Vector2(0.7f, 0.98f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Layout
            var hLayout = panelGO.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 10;
            hLayout.padding = new RectOffset(15, 15, 8, 8);
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = true;

            // Label "Name:"
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(panelGO.transform, false);
            var labelTmp = labelGO.AddComponent<TextMeshProUGUI>();
            labelTmp.text = "Name:";
            labelTmp.fontSize = 28;
            labelTmp.color = Color.white;
            labelTmp.alignment = TextAlignmentOptions.MidlineRight;
            labelTmp.enableWordWrapping = false;
            var labelLayout = labelGO.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 100;

            // Input field
            var inputGO = new GameObject("NameInput");
            inputGO.transform.SetParent(panelGO.transform, false);

            var inputBg = inputGO.AddComponent<Image>();
            inputBg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            m_NameInputField = inputGO.AddComponent<TMP_InputField>();
            m_NameInputField.characterLimit = 32;

            // Text area
            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputGO.transform, false);
            var textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 0);
            textAreaRect.offsetMax = new Vector2(-10, 0);
            var textAreaMask = textArea.AddComponent<RectMask2D>();

            // Input text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(textArea.transform, false);
            var inputText = textGO.AddComponent<TextMeshProUGUI>();
            inputText.fontSize = 26;
            inputText.color = Color.white;
            inputText.alignment = TextAlignmentOptions.MidlineLeft;
            inputText.enableWordWrapping = false;
            var textRectT = textGO.GetComponent<RectTransform>();
            textRectT.anchorMin = Vector2.zero;
            textRectT.anchorMax = Vector2.one;
            textRectT.offsetMin = Vector2.zero;
            textRectT.offsetMax = Vector2.zero;

            m_NameInputField.textViewport = textAreaRect;
            m_NameInputField.textComponent = inputText;

            // Placeholder
            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(textArea.transform, false);
            var placeholderText = placeholderGO.AddComponent<TextMeshProUGUI>();
            placeholderText.text = "Enter your name...";
            placeholderText.fontSize = 26;
            placeholderText.color = new Color(0.6f, 0.6f, 0.6f, 0.7f);
            placeholderText.fontStyle = FontStyles.Italic;
            placeholderText.alignment = TextAlignmentOptions.MidlineLeft;
            placeholderText.enableWordWrapping = false;
            var phRect = placeholderGO.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = Vector2.zero;
            phRect.offsetMax = Vector2.zero;
            m_NameInputField.placeholder = placeholderText;

            var inputLayout = inputGO.AddComponent<LayoutElement>();
            inputLayout.flexibleWidth = 1;

            // Confirm button
            var btnGO = new GameObject("ConfirmBtn");
            btnGO.transform.SetParent(panelGO.transform, false);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.6f, 0.2f, 1f);
            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(OnConfirmName);
            var btnLayout = btnGO.AddComponent<LayoutElement>();
            btnLayout.preferredWidth = 80;

            var btnTextGO = new GameObject("BtnText");
            btnTextGO.transform.SetParent(btnGO.transform, false);
            var btnTmp = btnTextGO.AddComponent<TextMeshProUGUI>();
            btnTmp.text = "OK";
            btnTmp.fontSize = 26;
            btnTmp.color = Color.white;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.enableWordWrapping = false;
            var btnTextRect = btnTextGO.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            // Submit name on Enter key
            m_NameInputField.onSubmit.AddListener(_ => OnConfirmName());

            // Pre-fill with current name if available
            string currentName = GetCurrentPlayerName();
            if (!string.IsNullOrEmpty(currentName) && currentName != "Player")
            {
                m_NameInputField.text = currentName;
            }

            // Force rebuild so TMP_InputField initializes correctly
            m_NameInputField.ForceLabelUpdate();
        }

        string GetCurrentPlayerName()
        {
            if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent(out PersistentPlayer pp))
            {
                return pp.NetworkNameState.Name;
            }
            return null;
        }

        void OnConfirmName()
        {
            if (m_NameInputField == null) return;
            string newName = m_NameInputField.text.Trim();
            if (string.IsNullOrWhiteSpace(newName)) return;

            if (NetworkClient.active && m_NetworkCharSelection != null)
            {
                m_NetworkCharSelection.CmdChangeName(newName);
                m_NameConfirmed = true;
            }
        }

        GameObject GetCharacterGraphics(Avatar avatar)
        {
            if (!m_SpawnedCharacterGraphics.TryGetValue(avatar.Guid, out GameObject characterGraphics))
            {
                characterGraphics = Instantiate(avatar.GraphicsCharacterSelect, m_CharacterGraphicsParent);
                m_SpawnedCharacterGraphics.Add(avatar.Guid, characterGraphics);
            }

            return characterGraphics;
        }

        void OnMatchResultChanged(string matchResultJson)
        {
            if (string.IsNullOrEmpty(matchResultJson)) return;
            if (m_MatchResultBanner != null) return; // already showing

            var result = JsonUtility.FromJson<MatchResult>(matchResultJson);
            if (result == null) return;

            long localClientId = NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent(out PersistentPlayer pp)
                ? (long)pp.OwnerConnectionId
                : 0L;

            bool isWinner = localClientId == result.winnerClientId;
            string bannerText = isWinner ? "YOU WON!" : $"YOU LOST — Winner: {result.winnerName}";
            Color bannerColor = isWinner ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);

            CreateMatchResultBanner(bannerText, bannerColor);
            StartCoroutine(CoroHideBanner());
        }

        void CreateMatchResultBanner(string text, Color color)
        {
            // Create canvas overlay for the banner
            var canvasGO = new GameObject("MatchResultBannerCanvas");
            m_MatchResultBanner = canvasGO;

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Background panel
            var panelGO = new GameObject("BannerPanel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0, 0, 0, 0.75f);

            var panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.15f, 0.75f);
            panelRect.anchorMax = new Vector2(0.85f, 0.92f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Text
            var textGO = new GameObject("BannerText");
            textGO.transform.SetParent(panelGO.transform, false);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 52;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = false;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black;

            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        IEnumerator CoroHideBanner()
        {
            yield return new WaitForSeconds(k_BannerDisplayDuration);
            if (m_MatchResultBanner != null)
            {
                Destroy(m_MatchResultBanner);
                m_MatchResultBanner = null;
            }
        }
    }
}
