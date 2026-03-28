using System;
using Mirror;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    public class ClientPlayerAvatar : NetworkBehaviour
    {
        [SerializeField]
        ClientPlayerAvatarRuntimeCollection m_PlayerAvatars;

        public static event Action<ClientPlayerAvatar> LocalClientSpawned;

        public static event Action LocalClientDespawned;

        bool m_AddedToCollection;

        public override void OnStartServer()
        {
            base.OnStartServer();
            name = "PlayerAvatar" + connectionToClient.connectionId;
            // On a dedicated server (no local client), add to collection here.
            // On host, defer to OnStartClient so isLocalPlayer is already set.
            if (!NetworkClient.active && !m_AddedToCollection)
            {
                if (m_PlayerAvatars) m_PlayerAvatars.Add(this);
                m_AddedToCollection = true;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!m_AddedToCollection)
            {
                if (m_PlayerAvatars) m_PlayerAvatars.Add(this);
                m_AddedToCollection = true;
            }
            if (isLocalPlayer)
            {
                LocalClientSpawned?.Invoke(this);
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (isLocalPlayer)
            {
                LocalClientDespawned?.Invoke();
            }
            if (!isServer)
            {
                RemoveNetworkCharacter();
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            RemoveNetworkCharacter();
        }

        void OnDestroy()
        {
            RemoveNetworkCharacter();
        }

        void RemoveNetworkCharacter()
        {
            if (m_AddedToCollection)
            {
                if (m_PlayerAvatars) m_PlayerAvatars.Remove(this);
                m_AddedToCollection = false;
            }
        }
    }
}
