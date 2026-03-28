using System.Collections.Generic;
using Mirror;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Infrastructure;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    /// <summary>
    /// Attached to the player-characters' prefab, this maintains a list of active ServerCharacter objects for players.
    /// </summary>
    /// <remarks>
    /// This is an optimization. In server code you can already get a list of players' ServerCharacters by
    /// iterating over the active connections and calling GetComponent() on their PlayerObject. But we need
    /// to iterate over all players quite often — the monsters' IdleAIState does so in every Update() —
    /// and all those GetComponent() calls add up! So this optimization lets us iterate without calling
    /// GetComponent(). This will be refactored with a ScriptableObject-based approach on player collection.
    /// </remarks>
    [RequireComponent(typeof(ServerCharacter))]
    public class PlayerServerCharacter : NetworkBehaviour
    {
        static List<ServerCharacter> s_ActivePlayers = new List<ServerCharacter>();

        [SerializeField]
        ServerCharacter m_CachedServerCharacter;

        public override void OnStartServer()
        {
            base.OnStartServer();
            s_ActivePlayers.Add(m_CachedServerCharacter);
        }

        void OnDisable()
        {
            s_ActivePlayers.Remove(m_CachedServerCharacter);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            ulong ownerClientId = connectionToClient != null ? (ulong)connectionToClient.connectionId : 0ul;
            var movementTransform = m_CachedServerCharacter.Movement.transform;
            SessionPlayerData? sessionPlayerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(ownerClientId);
            if (sessionPlayerData.HasValue)
            {
                var playerData = sessionPlayerData.Value;
                playerData.PlayerPosition = movementTransform.position;
                playerData.PlayerRotation = movementTransform.rotation;
                playerData.CurrentHitPoints = m_CachedServerCharacter.HitPoints;
                playerData.HasCharacterSpawned = true;
                SessionManager<SessionPlayerData>.Instance.SetPlayerData(ownerClientId, playerData);
            }
        }

        /// <summary>
        /// Returns a list of all active players' ServerCharacters. Treat the list as read-only!
        /// The list will be empty on the client.
        /// </summary>
        public static List<ServerCharacter> GetPlayerServerCharacters()
        {
            return s_ActivePlayers;
        }

        /// <summary>
        /// Returns the ServerCharacter owned by a specific client. Always returns null on the client.
        /// </summary>
        public static ServerCharacter GetPlayerServerCharacter(ulong ownerClientId)
        {
            foreach (var playerServerCharacter in s_ActivePlayers)
            {
                if (playerServerCharacter.connectionToClient != null &&
                    (ulong)playerServerCharacter.connectionToClient.connectionId == ownerClientId)
                {
                    return playerServerCharacter;
                }
            }
            return null;
        }
    }
}
