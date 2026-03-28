using System;
using Mirror;
using Unity.BossRoom.Gameplay.Configuration;
using Unity.BossRoom.Utils;

namespace Unity.BossRoom.Gameplay.GameState
{
    /// <summary>
    /// Common data and RPCs for the CharSelect stage.
    /// </summary>
    public class NetworkCharSelection : NetworkBehaviour
    {
        public enum SeatState : byte
        {
            Inactive,
            Active,
            LockedIn,
        }

        /// <summary>
        /// Describes one of the players in the session, and their current character-select status.
        /// </summary>
        public struct SessionPlayerState : IEquatable<SessionPlayerState>
        {
            public ulong ClientId;

            private FixedPlayerName m_PlayerName;

            public int PlayerNumber; // this player's assigned "P#". (0=P1, 1=P2, etc.)
            public int SeatIdx; // the latest seat they were in. -1 means none
            public float LastChangeTime;

            public SeatState SeatState;

            public SessionPlayerState(ulong clientId, string name, int playerNumber, SeatState state, int seatIdx = -1, float lastChangeTime = 0)
            {
                ClientId = clientId;
                PlayerNumber = playerNumber;
                SeatState = state;
                SeatIdx = seatIdx;
                LastChangeTime = lastChangeTime;
                m_PlayerName = new FixedPlayerName();
                PlayerName = name;
            }

            public string PlayerName
            {
                get => m_PlayerName;
                private set => m_PlayerName = value;
            }

            public bool Equals(SessionPlayerState other)
            {
                return ClientId == other.ClientId &&
                       m_PlayerName.Equals(other.m_PlayerName) &&
                       PlayerNumber == other.PlayerNumber &&
                       SeatIdx == other.SeatIdx &&
                       LastChangeTime.Equals(other.LastChangeTime) &&
                       SeatState == other.SeatState;
            }
        }

        // Mirror SyncList — must be readonly field
        public readonly SyncList<SessionPlayerState> sessionPlayers = new SyncList<SessionPlayerState>();

        public Avatar[] AvatarConfiguration;

        // ---- Match Result SyncVar (set when returning from PvP match) ----

        [SyncVar(hook = nameof(HandleMatchResultChanged))]
        string m_MatchResultJson;

        /// <summary>
        /// JSON with previous match result: {"winnerName":"...", "winnerClientId":123}
        /// Empty/null if this is the first round.
        /// </summary>
        public string MatchResultJson
        {
            get => m_MatchResultJson;
            set
            {
                string old = m_MatchResultJson;
                m_MatchResultJson = value;
                if (isServer) HandleMatchResultChanged(old, value);
            }
        }

        public event Action<string> MatchResultChanged;

        void HandleMatchResultChanged(string oldValue, string newValue)
        {
            MatchResultChanged?.Invoke(newValue);
        }

        // ---- IsSessionClosed SyncVar ----

        [SyncVar(hook = nameof(HandleIsSessionClosedChanged))]
        bool m_IsSessionClosed;

        /// <summary>
        /// When this becomes true, the session is closed and in process of terminating (switching to gameplay).
        /// </summary>
        public bool IsSessionClosed
        {
            get => m_IsSessionClosed;
            set
            {
                bool old = m_IsSessionClosed;
                m_IsSessionClosed = value;
                if (isServer) HandleIsSessionClosedChanged(old, value);
            }
        }

        /// <summary>Fired on both server and clients when IsSessionClosed changes.</summary>
        public event Action<bool, bool> IsSessionClosedChanged;

        void HandleIsSessionClosedChanged(bool oldValue, bool newValue)
        {
            IsSessionClosedChanged?.Invoke(oldValue, newValue);
        }

        // ---- Events ----

        /// <summary>
        /// Server notification when a client requests a different session-seat, or locks in their seat choice.
        /// </summary>
        public event Action<ulong, int, bool> OnClientChangedSeat;

        // ---- Commands ----

        /// <summary>
        /// Command to notify the server that a client has chosen a seat.
        /// requiresAuthority = false allows any client to call this.
        /// </summary>
        [Command(requiresAuthority = false)]
        public void ServerChangeSeatRpc(int seatIdx, bool lockedIn, NetworkConnectionToClient sender = null)
        {
            ulong clientId = sender != null ? (ulong)sender.connectionId : 0ul;
            OnClientChangedSeat?.Invoke(clientId, seatIdx, lockedIn);
        }

        /// <summary>
        /// Server notification when a client changes their display name.
        /// </summary>
        public event Action<ulong, string> OnClientChangedName;

        /// <summary>
        /// Command for clients to change their display name during character select.
        /// </summary>
        [Command(requiresAuthority = false)]
        public void ServerChangeNameRpc(string newName, NetworkConnectionToClient sender = null)
        {
            ulong clientId = sender != null ? (ulong)sender.connectionId : 0ul;
            OnClientChangedName?.Invoke(clientId, newName);
        }
    }

    /// <summary>
    /// Mirror NetworkReader / NetworkWriter extensions for SessionPlayerState.
    /// </summary>
    public static class SessionPlayerStateReaderWriterExtensions
    {
        public static void WriteSessionPlayerState(this NetworkWriter writer, NetworkCharSelection.SessionPlayerState state)
        {
            writer.WriteULong(state.ClientId);
            writer.WriteString(state.PlayerName);
            writer.WriteInt(state.PlayerNumber);
            writer.WriteByte((byte)state.SeatState);
            writer.WriteInt(state.SeatIdx);
            writer.WriteFloat(state.LastChangeTime);
        }

        public static NetworkCharSelection.SessionPlayerState ReadSessionPlayerState(this NetworkReader reader)
        {
            ulong clientId = reader.ReadULong();
            string name = reader.ReadString();
            int playerNumber = reader.ReadInt();
            var seatState = (NetworkCharSelection.SeatState)reader.ReadByte();
            int seatIdx = reader.ReadInt();
            float lastChangeTime = reader.ReadFloat();
            return new NetworkCharSelection.SessionPlayerState(clientId, name, playerNumber, seatState, seatIdx, lastChangeTime);
        }
    }
}
