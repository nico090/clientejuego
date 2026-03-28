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
        public int HitPoints;

        // public subscribable event to be invoked when HP has been fully depleted
        public event Action HitPointsDepleted;

        // public subscribable event to be invoked when HP has been replenished
        public event Action HitPointsReplenished;

        // public subscribable event to be invoked on every HP change (old, new)
        public event Action<int, int> HitPointsChanged;

        // SyncVar hook — called on clients when value is updated from server
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
