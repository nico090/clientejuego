using System;
using Mirror;
using Unity.BossRoom.Gameplay.UI;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameState
{
    /// <summary>
    /// Lightweight NetworkBehaviour that syncs PvP match data (timer, scores) to all clients.
    /// Lives on the BossRoomState prefab alongside NetworkHooks.
    /// Updated by PvPScoreManager on the server.
    /// </summary>
    public class PvPNetworkState : NetworkBehaviour
    {
        public static PvPNetworkState Instance { get; private set; }

        [SyncVar]
        float m_MatchTimeRemaining;

        [SyncVar]
        bool m_MatchActive;

        /// <summary>
        /// JSON-encoded scores, updated periodically by the server.
        /// </summary>
        [SyncVar(hook = nameof(OnScoresJsonChanged))]
        string m_ScoresJson;

        public float MatchTimeRemaining => m_MatchTimeRemaining;
        public bool MatchActive => m_MatchActive;
        public string ScoresJson => m_ScoresJson;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnScoresJsonChanged(string oldValue, string newValue)
        {
            // Ensure the HUD exists on clients when score data first arrives
            if (!string.IsNullOrEmpty(newValue))
            {
                PvPScoreHUD.EnsureExists();
            }
        }

        /// <summary>
        /// Called by PvPScoreManager on the server to push state to clients.
        /// </summary>
        [Server]
        public void UpdateFromServer(float timeRemaining, bool matchActive, string scoresJson)
        {
            m_MatchTimeRemaining = timeRemaining;
            m_MatchActive = matchActive;
            m_ScoresJson = scoresJson;

            // SyncVar hooks don't fire on the server/host, so ensure HUD exists here too
            if (!string.IsNullOrEmpty(scoresJson))
            {
                PvPScoreHUD.EnsureExists();
            }
        }
    }
}
