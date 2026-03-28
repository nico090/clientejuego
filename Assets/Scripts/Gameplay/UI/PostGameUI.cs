using System.Collections.Generic;
using Mirror;
using TMPro;
using Unity.BossRoom.Gameplay.GameState;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Unity.BossRoom.Gameplay.UI
{
    /// <summary>
    /// Provides backing logic for all of the UI that runs in the PostGame stage.
    /// Now displays a PvP scoreboard instead of Win/Loss messages.
    /// </summary>
    public class PostGameUI : MonoBehaviour
    {
        [SerializeField]
        private Light m_SceneLight;

        [SerializeField]
        private TextMeshProUGUI m_WinEndMessage;

        [SerializeField]
        private TextMeshProUGUI m_LoseGameMessage;

        [SerializeField]
        private GameObject m_ReplayButton;

        [SerializeField]
        private GameObject m_WaitOnHostMsg;

        [SerializeField]
        private Color m_WinLightColor;

        [SerializeField]
        private Color m_LoseLightColor;

        ServerPostGameState m_PostGameState;

        // Dynamically created scoreboard elements
        GameObject m_ScoreboardRoot;
        TextMeshProUGUI m_TitleText;
        readonly List<TextMeshProUGUI> m_EntryTexts = new List<TextMeshProUGUI>();

        [Inject]
        void Inject(ServerPostGameState postGameState)
        {
            m_PostGameState = postGameState;

            bool isHost = NetworkServer.active && NetworkClient.active;
            if (isHost)
            {
                m_ReplayButton.SetActive(true);
                m_WaitOnHostMsg.SetActive(false);
            }
            else
            {
                m_ReplayButton.SetActive(false);
                m_WaitOnHostMsg.SetActive(true);
            }
        }

        void Start()
        {
            // Hide old Win/Loss messages
            m_WinEndMessage.gameObject.SetActive(false);
            m_LoseGameMessage.gameObject.SetActive(false);

            // Set light to win color (match completed)
            m_SceneLight.color = m_WinLightColor;

            // Subscribe to scores arriving
            m_PostGameState.NetworkPostGame.FinalScoresChanged += OnFinalScoresChanged;

            // If scores are already set, display them
            string json = m_PostGameState.NetworkPostGame.FinalScoresJson;
            if (!string.IsNullOrEmpty(json))
            {
                BuildScoreboard(json);
            }
        }

        void OnDestroy()
        {
            if (m_PostGameState != null)
            {
                m_PostGameState.NetworkPostGame.FinalScoresChanged -= OnFinalScoresChanged;
            }
        }

        void OnFinalScoresChanged(string scoresJson)
        {
            if (!string.IsNullOrEmpty(scoresJson))
            {
                BuildScoreboard(scoresJson);
            }
        }

        void BuildScoreboard(string json)
        {
            // Clean up previous scoreboard if rebuilt
            if (m_ScoreboardRoot != null)
            {
                Destroy(m_ScoreboardRoot);
                m_EntryTexts.Clear();
            }

            var scoreBoard = JsonUtility.FromJson<PvPScoreManager.ScoreBoard>(json);
            if (scoreBoard == null || scoreBoard.entries == null) return;

            // Create scoreboard container as child of this UI
            m_ScoreboardRoot = new GameObject("PvPScoreboard");
            m_ScoreboardRoot.transform.SetParent(transform, false);

            var rootRect = m_ScoreboardRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(0, 50);
            rootRect.sizeDelta = new Vector2(500, 400);

            var layout = m_ScoreboardRoot.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 8;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = m_ScoreboardRoot.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(m_ScoreboardRoot.transform, false);
            m_TitleText = titleGO.AddComponent<TextMeshProUGUI>();
            m_TitleText.text = "MATCH RESULTS";
            m_TitleText.fontSize = 48;
            m_TitleText.alignment = TextAlignmentOptions.Center;
            m_TitleText.color = Color.yellow;
            m_TitleText.fontStyle = FontStyles.Bold;
            titleGO.GetComponent<RectTransform>().sizeDelta = new Vector2(500, 60);

            // Entries (already sorted by score descending)
            for (int i = 0; i < scoreBoard.entries.Count; i++)
            {
                var entry = scoreBoard.entries[i];
                var entryGO = new GameObject($"Entry_{i}");
                entryGO.transform.SetParent(m_ScoreboardRoot.transform, false);

                var txt = entryGO.AddComponent<TextMeshProUGUI>();
                string rank = (i + 1).ToString();
                string medal = i == 0 ? "  <<" : "";
                txt.text = $"#{rank}  {entry.playerName}  —  {entry.score} pts{medal}";
                txt.fontSize = i == 0 ? 36 : 30;
                txt.alignment = TextAlignmentOptions.Center;
                txt.color = i == 0 ? Color.yellow : Color.white;

                entryGO.GetComponent<RectTransform>().sizeDelta = new Vector2(500, 44);
                m_EntryTexts.Add(txt);
            }
        }

        public void OnPlayAgainClicked()
        {
            m_PostGameState.PlayAgain();
        }

        public void OnMainMenuClicked()
        {
            m_PostGameState.GoToMainMenu();
        }
    }
}
