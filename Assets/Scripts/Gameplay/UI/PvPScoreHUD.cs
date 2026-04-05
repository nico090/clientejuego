using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameState;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Unity.BossRoom.Gameplay.UI
{
    /// <summary>
    /// Runtime-generated HUD overlay that shows PvP match timer and per-player scores.
    /// The scoreboard is hidden by default and shown while holding Tab (CS-style).
    /// Timer is always visible at the top center.
    /// </summary>
    public class PvPScoreHUD : MonoBehaviour
    {
        static PvPScoreHUD s_Instance;

        Canvas m_Canvas;
        TextMeshProUGUI m_TimerText;

        // Scoreboard panel (shown on Tab)
        GameObject m_ScoreboardRoot;
        VerticalLayoutGroup m_ScoreList;
        TextMeshProUGUI m_HeaderText;
        readonly List<TextMeshProUGUI> m_ScoreEntries = new List<TextMeshProUGUI>();

        public static void EnsureExists()
        {
            if (s_Instance != null) return;
            // Only create the HUD when PvPNetworkState is active (i.e. during gameplay scene)
            if (PvPNetworkState.Instance == null) return;
            var go = new GameObject("PvPScoreHUD");
            s_Instance = go.AddComponent<PvPScoreHUD>();
        }

        void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
        }

        void Start()
        {
            BuildUI();
        }

        void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        void Update()
        {
            var netState = PvPNetworkState.Instance;
            if (netState == null) return;

            // ── Toggle scoreboard with Tab ──
            bool showBoard = Input.GetKey(KeyCode.Tab);
            if (m_ScoreboardRoot.activeSelf != showBoard)
                m_ScoreboardRoot.SetActive(showBoard);

            // ── Update timer (always visible) ──
            float t = netState.MatchTimeRemaining;
            int minutes = Mathf.FloorToInt(t / 60f);
            int seconds = Mathf.FloorToInt(t % 60f);
            m_TimerText.text = $"{minutes}:{seconds:00}";

            if (!netState.MatchActive && t <= 0f)
            {
                m_TimerText.text = "MATCH OVER";
                // Keep scoreboard visible when match ends
                if (!m_ScoreboardRoot.activeSelf)
                    m_ScoreboardRoot.SetActive(true);
            }

            // ── Update scores (even when hidden, so data is fresh on open) ──
            string json = netState.ScoresJson;
            if (string.IsNullOrEmpty(json)) return;

            var board = JsonUtility.FromJson<PvPScoreManager.ScoreBoard>(json);
            if (board == null || board.entries == null) return;

            while (m_ScoreEntries.Count < board.entries.Count)
                CreateScoreEntry();

            for (int i = 0; i < m_ScoreEntries.Count; i++)
            {
                if (i < board.entries.Count)
                {
                    m_ScoreEntries[i].gameObject.SetActive(true);
                    var e = board.entries[i];
                    m_ScoreEntries[i].text = $"{e.playerName}    {e.score}";
                }
                else
                {
                    m_ScoreEntries[i].gameObject.SetActive(false);
                }
            }
        }

        void BuildUI()
        {
            // ── Canvas ──
            var canvasGO = new GameObject("PvPScoreCanvas");
            canvasGO.transform.SetParent(transform);
            m_Canvas = canvasGO.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_Canvas.sortingOrder = 100;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Timer (always visible, top center) ──
            var timerGO = new GameObject("Timer");
            timerGO.transform.SetParent(canvasGO.transform, false);
            m_TimerText = timerGO.AddComponent<TextMeshProUGUI>();
            m_TimerText.text = "5:00";
            m_TimerText.fontSize = 42;
            m_TimerText.alignment = TextAlignmentOptions.Center;
            m_TimerText.color = Color.white;
            m_TimerText.enableWordWrapping = false;
            m_TimerText.outlineWidth = 0.2f;
            m_TimerText.outlineColor = Color.black;

            var timerRect = timerGO.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 1f);
            timerRect.anchorMax = new Vector2(0.5f, 1f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.anchoredPosition = new Vector2(0, -10);
            timerRect.sizeDelta = new Vector2(300, 60);

            // ── Scoreboard panel (centered, hidden by default) ──
            m_ScoreboardRoot = new GameObject("ScoreboardRoot");
            m_ScoreboardRoot.transform.SetParent(canvasGO.transform, false);

            var rootRect = m_ScoreboardRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(0, 40);
            rootRect.sizeDelta = new Vector2(420, 0); // width fixed, height auto

            // Background image
            var bgImage = m_ScoreboardRoot.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.75f);

            // Vertical layout on the root itself
            var rootLayout = m_ScoreboardRoot.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(20, 20, 16, 16);
            rootLayout.spacing = 6;
            rootLayout.childAlignment = TextAnchor.UpperCenter;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            var rootFitter = m_ScoreboardRoot.AddComponent<ContentSizeFitter>();
            rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Header row ──
            var headerGO = new GameObject("Header");
            headerGO.transform.SetParent(m_ScoreboardRoot.transform, false);
            m_HeaderText = headerGO.AddComponent<TextMeshProUGUI>();
            m_HeaderText.text = "SCOREBOARD";
            m_HeaderText.fontSize = 30;
            m_HeaderText.fontStyle = FontStyles.Bold;
            m_HeaderText.alignment = TextAlignmentOptions.Center;
            m_HeaderText.color = new Color(1f, 0.85f, 0.2f); // gold
            m_HeaderText.enableWordWrapping = false;

            var headerLE = headerGO.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 40;

            // ── Separator ──
            var sepGO = new GameObject("Separator");
            sepGO.transform.SetParent(m_ScoreboardRoot.transform, false);
            var sepImg = sepGO.AddComponent<Image>();
            sepImg.color = new Color(1f, 1f, 1f, 0.3f);
            var sepLE = sepGO.AddComponent<LayoutElement>();
            sepLE.preferredHeight = 2;

            // ── Column headers ──
            var colGO = new GameObject("ColumnHeaders");
            colGO.transform.SetParent(m_ScoreboardRoot.transform, false);
            var colText = colGO.AddComponent<TextMeshProUGUI>();
            colText.text = "Player                        Score";
            colText.fontSize = 20;
            colText.fontStyle = FontStyles.Bold;
            colText.alignment = TextAlignmentOptions.Center;
            colText.color = new Color(0.7f, 0.7f, 0.7f);
            colText.enableWordWrapping = false;
            var colLE = colGO.AddComponent<LayoutElement>();
            colLE.preferredHeight = 28;

            // ── Score list container ──
            var scorePanel = new GameObject("ScoreList");
            scorePanel.transform.SetParent(m_ScoreboardRoot.transform, false);

            m_ScoreList = scorePanel.AddComponent<VerticalLayoutGroup>();
            m_ScoreList.spacing = 4;
            m_ScoreList.childAlignment = TextAnchor.UpperCenter;
            m_ScoreList.childControlWidth = true;
            m_ScoreList.childControlHeight = true;
            m_ScoreList.childForceExpandWidth = true;
            m_ScoreList.childForceExpandHeight = false;

            var listFitter = scorePanel.AddComponent<ContentSizeFitter>();
            listFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Hint text ──
            var hintGO = new GameObject("Hint");
            hintGO.transform.SetParent(m_ScoreboardRoot.transform, false);
            var hintText = hintGO.AddComponent<TextMeshProUGUI>();
            hintText.text = "[TAB]";
            hintText.fontSize = 16;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.color = new Color(1f, 1f, 1f, 0.35f);
            hintText.enableWordWrapping = false;
            var hintLE = hintGO.AddComponent<LayoutElement>();
            hintLE.preferredHeight = 24;

            // Start hidden
            m_ScoreboardRoot.SetActive(false);
        }

        void CreateScoreEntry()
        {
            var entryGO = new GameObject($"Score_{m_ScoreEntries.Count}");
            entryGO.transform.SetParent(m_ScoreList.transform, false);

            var txt = entryGO.AddComponent<TextMeshProUGUI>();
            txt.fontSize = 24;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
            txt.enableWordWrapping = false;

            var entryLE = entryGO.AddComponent<LayoutElement>();
            entryLE.preferredHeight = 32;

            m_ScoreEntries.Add(txt);
        }
    }
}
