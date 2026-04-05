using Mirror;
using UnityEngine;

namespace Unity.BossRoom.Infrastructure
{
    /// <summary>
    /// NetworkBehaviour spawned per-client that tracks that client's scene-loading progress.
    /// The owning client updates its progress via a Command, and the value is synced
    /// to all clients via SyncVar so the host/UI can display a loading bar.
    /// </summary>
    public class NetworkedLoadingProgressTracker : NetworkBehaviour
    {
        [SyncVar]
        float m_Progress;

        LoadingProgressManager m_CachedManager;

        /// <summary>Current loading progress for this client (0 to 1).</summary>
        public float Progress => m_Progress;

        public override void OnStartServer()
        {
            base.OnStartServer();
            m_Progress = 0f;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (isOwned)
            {
                m_CachedManager = FindObjectOfType<LoadingProgressManager>();
                if (m_CachedManager != null)
                {
                    ulong clientId = connectionToServer != null ? 0ul : (ulong)connectionToClient.connectionId;
                    m_CachedManager.AddTracker(clientId, this);
                }
            }
        }

        /// <summary>
        /// Called by the owning client to update its loading progress.
        /// </summary>
        [Command]
        public void CmdSetProgress(float progress)
        {
            m_Progress = Mathf.Clamp01(progress);
        }

        void Update()
        {
            if (isOwned && !isServer && m_CachedManager != null)
            {
                if (Mathf.Abs(m_CachedManager.LocalProgress - m_Progress) > 0.01f)
                {
                    CmdSetProgress(m_CachedManager.LocalProgress);
                }
            }
        }
    }
}
