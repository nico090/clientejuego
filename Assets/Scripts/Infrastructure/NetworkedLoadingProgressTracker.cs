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
                // Register with the LoadingProgressManager if available
                var manager = FindObjectOfType<LoadingProgressManager>();
                if (manager != null)
                {
                    ulong clientId = connectionToServer != null ? 0ul : (ulong)connectionToClient.connectionId;
                    manager.AddTracker(clientId, this);
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
            if (isOwned && !isServer)
            {
                // Sync local async loading progress to server
                var manager = FindObjectOfType<LoadingProgressManager>();
                if (manager != null && Mathf.Abs(manager.LocalProgress - m_Progress) > 0.01f)
                {
                    CmdSetProgress(manager.LocalProgress);
                }
            }
        }
    }
}
