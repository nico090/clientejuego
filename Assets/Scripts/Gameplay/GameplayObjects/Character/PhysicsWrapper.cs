using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    /// <summary>
    /// Wrapper class for direct references to components relevant to physics.
    /// Each instance of a PhysicsWrapper is registered to a static dictionary, indexed by the NetworkIdentity's netId.
    /// </summary>
    /// <remarks>
    /// The root GameObject of PCs and NPCs is not the object which will move through the world, so other classes
    /// need a quick reference to a PC's/NPC's in-game position.
    /// </remarks>
    public class PhysicsWrapper : NetworkBehaviour
    {
        static Dictionary<uint, PhysicsWrapper> m_PhysicsWrappers = new Dictionary<uint, PhysicsWrapper>();

        [SerializeField]
        Transform m_Transform;

        public Transform Transform => m_Transform;

        [SerializeField]
        Collider m_DamageCollider;

        public Collider DamageCollider => m_DamageCollider;

        uint m_NetworkObjectID;

        public override void OnStartServer()
        {
            base.OnStartServer();
            Register();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!isServer)
            {
                Register();
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            RemovePhysicsWrapper();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!isServer)
            {
                RemovePhysicsWrapper();
            }
        }

        void OnDestroy()
        {
            RemovePhysicsWrapper();
        }

        void Register()
        {
            m_NetworkObjectID = netId;
            m_PhysicsWrappers[m_NetworkObjectID] = this;
        }

        void RemovePhysicsWrapper()
        {
            m_PhysicsWrappers.Remove(m_NetworkObjectID);
        }

        public static bool TryGetPhysicsWrapper(uint networkObjectID, out PhysicsWrapper physicsWrapper)
        {
            return m_PhysicsWrappers.TryGetValue(networkObjectID, out physicsWrapper);
        }
    }
}
