using System;
using Mirror;
using VContainer;

namespace Unity.BossRoom.Gameplay.GameState
{
    public class NetworkPostGame : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnWinStateChanged))]
        WinState m_WinState;

        /// <summary>
        /// JSON string containing the final PvP scoreboard (serialized ScoreBoard).
        /// Synced to all clients so PostGameUI can display it.
        /// </summary>
        [SyncVar(hook = nameof(OnFinalScoresChanged))]
        string m_FinalScoresJson;

        public WinState WinState
        {
            get => m_WinState;
            set
            {
                var old = m_WinState;
                m_WinState = value;
                if (isServer) OnWinStateChanged(old, value);
            }
        }

        public string FinalScoresJson
        {
            get => m_FinalScoresJson;
            set
            {
                var old = m_FinalScoresJson;
                m_FinalScoresJson = value;
                if (isServer) OnFinalScoresChanged(old, value);
            }
        }

        /// <summary>Fired on both server and clients when WinState changes.</summary>
        public event Action<WinState, WinState> WinStateChanged;

        /// <summary>Fired on both server and clients when FinalScoresJson changes.</summary>
        public event Action<string> FinalScoresChanged;

        void OnWinStateChanged(WinState oldValue, WinState newValue)
        {
            WinStateChanged?.Invoke(oldValue, newValue);
        }

        void OnFinalScoresChanged(string oldValue, string newValue)
        {
            FinalScoresChanged?.Invoke(newValue);
        }

        [Inject]
        public void Construct(PersistentGameState persistentGameState)
        {
            if (NetworkServer.active)
            {
                WinState = persistentGameState.WinState;
            }
        }
    }
}
