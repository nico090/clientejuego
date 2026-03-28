using Mirror;
using UnityEngine;

namespace Unity.Multiplayer.Samples.BossRoom.Client
{
    /// <summary>
    /// Component to play VFX and SFX when this NetworkObject's parent changes, making the action look more polished.
    /// Mirror replicates NetworkIdentity parenting to clients, which triggers Unity's built-in
    /// OnTransformParentChanged callback — used here in place of NGO's OnNetworkObjectParentChanged.
    /// </summary>
    public class ClientPickUpPotEffects : NetworkBehaviour
    {
        [SerializeField]
        ParticleSystem m_PutDownParticleSystem;

        [SerializeField]
        AudioSource m_PickUpSound;

        [SerializeField]
        AudioSource m_PutDownSound;

        void Awake()
        {
            enabled = false;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            enabled = true;
        }

        /// <summary>
        /// Called by Unity whenever this object's Transform parent changes.
        /// Because Mirror replicates NetworkIdentity parenting, this fires on clients
        /// when the server reparents the pot (pick up / put down).
        /// </summary>
        void OnTransformParentChanged()
        {
            if (!isClient)
            {
                return;
            }

            var parentNetworkIdentity = transform.parent?.GetComponentInParent<NetworkIdentity>();
            if (parentNetworkIdentity == null)
            {
                m_PutDownParticleSystem.Play();
                m_PutDownSound.Play();
            }
            else
            {
                m_PickUpSound.Play();
            }
        }
    }
}
