using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Unity.BossRoom.Infrastructure
{
    /// <summary>
    /// Manages networked loading progress tracking. Spawns a NetworkedLoadingProgressTracker
    /// for each connected client so the host can monitor everyone's scene-loading progress.
    /// </summary>
    public class LoadingProgressManager : MonoBehaviour
    {
        [SerializeField]
        GameObject m_ProgressTrackerPrefab;

        readonly Dictionary<ulong, NetworkedLoadingProgressTracker> m_TrackersByClientId =
            new Dictionary<ulong, NetworkedLoadingProgressTracker>();

        public IReadOnlyDictionary<ulong, NetworkedLoadingProgressTracker> TrackersByClientId => m_TrackersByClientId;

        /// <summary>
        /// The local client's loading progress (0 to 1).
        /// </summary>
        public float LocalProgress { get; set; }

        public void AddTracker(ulong clientId, NetworkedLoadingProgressTracker tracker)
        {
            m_TrackersByClientId[clientId] = tracker;
        }

        public void RemoveTracker(ulong clientId)
        {
            m_TrackersByClientId.Remove(clientId);
        }

        public void ResetTrackers()
        {
            m_TrackersByClientId.Clear();
            LocalProgress = 0f;
        }
    }
}
