using System;
using Mirror;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Utils;
using Unity.BossRoom.Infrastructure;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
    /// <summary>
    /// NetworkBehaviour that represents a player connection. This object persists between scenes
    /// for the duration of the connection.
    /// </summary>
    /// <remarks>
    /// In Mirror, DontDestroyOnLoad must be called explicitly if scene persistence is desired.
    /// </remarks>
    public class PersistentPlayer : NetworkBehaviour
    {
        [SerializeField]
        PersistentPlayerRuntimeCollection m_PersistentPlayerRuntimeCollection;

        [SerializeField]
        NetworkNameState m_NetworkNameState;

        [SerializeField]
        NetworkAvatarGuidState m_NetworkAvatarGuidState;

        public NetworkNameState NetworkNameState => m_NetworkNameState;

        public NetworkAvatarGuidState NetworkAvatarGuidState => m_NetworkAvatarGuidState;

        [SyncVar]
        ulong m_OwnerConnectionId;

        /// <summary>Mirror equivalent of NGO's OwnerConnectionId. Synced to all clients.</summary>
        public ulong OwnerConnectionId => m_OwnerConnectionId;

        // Tracks whether we have been added to the runtime collection to avoid double-adding on host.
        bool m_AddedToCollection;

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Move to DontDestroyOnLoad so this object survives scene changes.
            // Mirror does NOT do this automatically (unlike NGO).
            DontDestroyOnLoad(gameObject);

            gameObject.name = "PersistentPlayer" + connectionToClient.connectionId;
            ulong ownerClientId = (ulong)connectionToClient.connectionId;
            m_OwnerConnectionId = ownerClientId;

            if (!m_AddedToCollection)
            {
                m_PersistentPlayerRuntimeCollection.Add(this);
                m_AddedToCollection = true;
            }

            var sessionPlayerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(ownerClientId);
            if (sessionPlayerData.HasValue)
            {
                var playerData = sessionPlayerData.Value;
                m_NetworkNameState.SetName(playerData.PlayerName);
                if (playerData.HasCharacterSpawned)
                {
                    m_NetworkAvatarGuidState.AvatarGuid = playerData.AvatarNetworkGuid;
                }
                else
                {
                    m_NetworkAvatarGuidState.SetRandomAvatar();
                    playerData.AvatarNetworkGuid = m_NetworkAvatarGuidState.AvatarGuid;
                    SessionManager<SessionPlayerData>.Instance.SetPlayerData(ownerClientId, playerData);
                }
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // On a pure client, move to DDOL so this object survives scene changes.
            // (On host, OnStartServer already did this.)
            if (!isServer)
                DontDestroyOnLoad(gameObject);

            // Avoid double-adding on host (server already handled it in OnStartServer).
            if (!m_AddedToCollection)
            {
                m_PersistentPlayerRuntimeCollection.Add(this);
                m_AddedToCollection = true;
            }
        }

        void OnDestroy()
        {
            RemovePersistentPlayer();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            SaveAndRemove();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!isServer)
            {
                RemovePersistentPlayer();
            }
        }

        void SaveAndRemove()
        {
            ulong ownerClientId = connectionToClient != null ? (ulong)connectionToClient.connectionId : 0ul;
            RemovePersistentPlayer();

            var sessionPlayerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(ownerClientId);
            if (sessionPlayerData.HasValue)
            {
                var playerData = sessionPlayerData.Value;
                playerData.PlayerName = m_NetworkNameState.Name;
                playerData.AvatarNetworkGuid = m_NetworkAvatarGuidState.AvatarGuid;
                SessionManager<SessionPlayerData>.Instance.SetPlayerData(ownerClientId, playerData);
            }
        }

        void RemovePersistentPlayer()
        {
            if (m_AddedToCollection)
            {
                m_PersistentPlayerRuntimeCollection.Remove(this);
                m_AddedToCollection = false;
            }
        }
    }
}
