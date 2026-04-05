using System;
using Mirror;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
    /// <summary>
    /// MonoBehaviour containing only one SyncVar int which represents this object's health.
    /// </summary>
    public class NetworkHealthState : NetworkBehaviour
    {
        [HideInInspector]
        [SyncVar(hook = nameof(OnHitPointsChanged))]
        int m_HitPoints;

        /// <summary>
        /// Current hit points. Write on server; fires events on both server and clients.
        /// Mirror does not call SyncVar hooks on the server, so the property setter
        /// fires the hook manually when isServer is true.
        /// </summary>
        public int HitPoints
        {
            get => m_HitPoints;
            set
            {
                var old = m_HitPoints;
                m_HitPoints = value;
                if (isServer) OnHitPointsChanged(old, value);
            }
        }

        // public subscribable event to be invoked when HP has been fully depleted
        public event Action HitPointsDepleted;

        // public subscribable event to be invoked when HP has been replenished
        public event Action HitPointsReplenished;

        // public subscribable event to be invoked on every HP change (old, new)
        public event Action<int, int> HitPointsChanged;

        // SyncVar hook — called on clients when value is updated from server,
        // and manually on the server via the property setter.
        void OnHitPointsChanged(int previousValue, int newValue)
        {
            HitPointsChanged?.Invoke(previousValue, newValue);

            if (previousValue > 0 && newValue <= 0)
            {
                // newly reached 0 HP
                HitPointsDepleted?.Invoke();
            }
            else if (previousValue <= 0 && newValue > 0)
            {
                // newly revived
                HitPointsReplenished?.Invoke();
            }
        }
    }
}
