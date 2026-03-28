using Mirror;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    /// <summary>
    /// Subclass of Mirror's NetworkAnimator that instantiates the player avatar's Graphics GameObject
    /// on clients when the object is spawned, then binds the Animator before Mirror applies
    /// the initial animation state.
    /// </summary>
    public class ClientPlayerAvatarNetworkAnimator : NetworkAnimator
    {
        [SerializeField]
        NetworkAvatarGuidState m_NetworkAvatarGuidState;

        bool m_AvatarInstantiated;

        /// <summary>Exposes the underlying Animator for other components (e.g. ClientCharacter).</summary>
        public Animator Animator => animator;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!m_AvatarInstantiated)
            {
                InstantiateAvatar();
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            m_AvatarInstantiated = false;
            if (animator != null && animator.transform.childCount > 0)
            {
                var avatarGraphics = animator.transform.GetChild(0);
                if (avatarGraphics != null)
                {
                    Destroy(avatarGraphics.gameObject);
                }
            }
        }

        void InstantiateAvatar()
        {
            if (animator == null || animator.transform.childCount > 0)
            {
                // Animator not ready, or avatar already instantiated — skip.
                return;
            }

            // Spawn avatar graphics GameObject
            Instantiate(m_NetworkAvatarGuidState.RegisteredAvatar.Graphics, animator.transform);

            animator.Rebind();

            m_AvatarInstantiated = true;
        }
    }
}
