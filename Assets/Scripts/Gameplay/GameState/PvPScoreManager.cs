using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameState
{
    /// <summary>
    /// Server-only PvP score manager. Tracks per-player scores and match timer.
    /// Pushes state to PvPNetworkState for client sync.
    /// </summary>
    public class PvPScoreManager : MonoBehaviour
    {
        public static PvPScoreManager Instance { get; private set; }

        // ── Scoring constants ──
        public const int PointsPerNpcKill = 1;
        public const int PointsPerPlayerKill = 3;
        public const int PenaltyDeathByNpc = -3;
        public const float DefaultMatchDuration = 300f; // 5 minutes

        // ── Server state ──
        readonly Dictionary<uint, int> m_Scores = new Dictionary<uint, int>();
        readonly Dictionary<uint, string> m_PlayerNames = new Dictionary<uint, string>();

        float m_MatchTimeRemaining;
        bool m_MatchActive;
        string m_SerializedFinalScores;

        // ── Sync throttle ──
        const float k_SyncInterval = 0.25f;
        float m_NextSyncTime;
        bool m_ScoresDirty;

        // ── Events ──
        public event Action MatchEnded;

        // ── Public accessors ──
        public float MatchTimeRemaining => m_MatchTimeRemaining;
        public bool MatchActive => m_MatchActive;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PvPScoreManager] Duplicate instance destroyed");
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ──────────────────────────────────────────────
        // Server API
        // ──────────────────────────────────────────────

        public void StartMatch(float duration = DefaultMatchDuration)
        {
            m_MatchTimeRemaining = duration;
            m_MatchActive = true;
            Debug.Log($"[PvP] Match started — {duration}s");

            // Push initial state immediately so clients get scores on first frame
            SyncToNetwork();
        }

        public void RegisterPlayer(uint netId, string playerName)
        {
            if (!m_Scores.ContainsKey(netId))
            {
                m_Scores[netId] = 0;
                m_PlayerNames[netId] = playerName;
                Debug.Log($"[PvP] Registered player '{playerName}' (netId={netId})");
            }
        }

        public void UnregisterPlayer(uint netId)
        {
            m_Scores.Remove(netId);
            m_PlayerNames.Remove(netId);
        }

        public void OnPlayerKilledNpc(uint killerNetId)
        {
            AddScore(killerNetId, PointsPerNpcKill);
        }

        public void OnPlayerKilledPlayer(uint killerNetId)
        {
            AddScore(killerNetId, PointsPerPlayerKill);
        }

        public void OnPlayerKilledByNpc(uint victimNetId)
        {
            AddScore(victimNetId, PenaltyDeathByNpc);
        }

        // ──────────────────────────────────────────────
        // Timer
        // ──────────────────────────────────────────────

        void Update()
        {
            if (!m_MatchActive) return;

            m_MatchTimeRemaining -= Time.deltaTime;
            if (m_MatchTimeRemaining <= 0f)
            {
                m_MatchTimeRemaining = 0f;
                EndMatch();
                return;
            }

            // Throttle network sync to avoid per-frame JSON serialization
            if (Time.time >= m_NextSyncTime || m_ScoresDirty)
            {
                m_ScoresDirty = false;
                m_NextSyncTime = Time.time + k_SyncInterval;
                SyncToNetwork();
            }
        }

        // ──────────────────────────────────────────────
        // Network sync
        // ──────────────────────────────────────────────

        void SyncToNetwork()
        {
            if (PvPNetworkState.Instance == null) return;
            PvPNetworkState.Instance.UpdateFromServer(m_MatchTimeRemaining, m_MatchActive, BuildScoresJson());
        }

        string BuildScoresJson()
        {
            var entries = new List<ScoreEntry>();
            foreach (var kvp in m_Scores)
            {
                string name = m_PlayerNames.TryGetValue(kvp.Key, out var n) ? n : $"Player {kvp.Key}";
                entries.Add(new ScoreEntry { playerName = name, score = kvp.Value });
            }
            entries.Sort((a, b) => b.score.CompareTo(a.score));
            return JsonUtility.ToJson(new ScoreBoard { entries = entries });
        }

        // ──────────────────────────────────────────────
        // Match end
        // ──────────────────────────────────────────────

        void EndMatch()
        {
            if (!m_MatchActive) return;
            m_MatchActive = false;

            m_SerializedFinalScores = BuildScoresJson();
            SyncToNetwork();

            Debug.Log($"[PvP] Match ended. Scores: {m_SerializedFinalScores}");
            MatchEnded?.Invoke();
        }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────

        void AddScore(uint netId, int points)
        {
            if (m_Scores.ContainsKey(netId))
            {
                m_Scores[netId] += points;
                m_ScoresDirty = true;
            }
        }

        public uint GetWinnerNetId()
        {
            uint winner = 0;
            int best = int.MinValue;
            foreach (var kvp in m_Scores)
            {
                if (kvp.Value > best)
                {
                    best = kvp.Value;
                    winner = kvp.Key;
                }
            }
            return winner;
        }

        public string GetWinnerName()
        {
            uint winnerId = GetWinnerNetId();
            return m_PlayerNames.TryGetValue(winnerId, out var name) ? name : "Nobody";
        }

        public string GetFinalScoresJson() => m_SerializedFinalScores;

        public int GetScore(uint netId)
        {
            return m_Scores.TryGetValue(netId, out int score) ? score : 0;
        }

        // ──────────────────────────────────────────────
        // Serialization helpers
        // ──────────────────────────────────────────────

        [Serializable]
        public class ScoreEntry
        {
            public string playerName;
            public int score;
        }

        [Serializable]
        public class ScoreBoard
        {
            public List<ScoreEntry> entries;
        }
    }
}
