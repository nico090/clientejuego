using System;
using Mirror;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
    public enum LifeState
    {
        Alive,
        Fainted,
        Dead,
    }

    /// <summary>
    /// MonoBehaviour containing SyncVars for LifeState (and optionally IsGodMode).
    /// Fires C# events on both server (via property setter) and clients (via SyncVar hook).
    /// </summary>
    public class NetworkLifeState : NetworkBehaviour
    {
        [SyncVar(hook = nameof(HandleLifeStateChanged))]
        LifeState m_LifeState = GameplayObjects.LifeState.Alive;

        /// <summary>
        /// Gets or sets the current life state.
        /// Write only on server; fires <see cref="LifeStateChanged"/> on the server side.
        /// Clients receive updates via SyncVar and the hook fires <see cref="LifeStateChanged"/> there too.
        /// </summary>
        public LifeState LifeState
        {
            get => m_LifeState;
            set
            {
                var old = m_LifeState;
                m_LifeState = value;
                // Mirror does not call the hook on the server when the SyncVar is set,
                // so we fire the event manually here.
                if (isServer) HandleLifeStateChanged(old, value);
            }
        }

        /// <summary>Fired on both server and clients whenever LifeState changes.</summary>
        public event Action<LifeState, LifeState> LifeStateChanged;

        void HandleLifeStateChanged(LifeState previousValue, LifeState newValue)
        {
            LifeStateChanged?.Invoke(previousValue, newValue);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SyncVar(hook = nameof(HandleIsGodModeChanged))]
        bool m_IsGodMode = false;

        /// <summary>
        /// Indicates whether this character is in "god mode" (cannot be damaged).
        /// Write only on server.
        /// </summary>
        public bool IsGodMode
        {
            get => m_IsGodMode;
            set
            {
                bool old = m_IsGodMode;
                m_IsGodMode = value;
                if (isServer) HandleIsGodModeChanged(old, value);
            }
        }

        /// <summary>Fired on both server and clients whenever IsGodMode changes.</summary>
        public event Action<bool, bool> IsGodModeChanged;

        void HandleIsGodModeChanged(bool previousValue, bool newValue)
        {
            IsGodModeChanged?.Invoke(previousValue, newValue);
        }
#endif
    }
}
