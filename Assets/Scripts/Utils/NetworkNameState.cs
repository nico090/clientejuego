using System;
using Mirror;
using UnityEngine;

namespace Unity.BossRoom.Utils
{
    /// <summary>
    /// NetworkBehaviour containing only one SyncVar which represents this object's name.
    /// </summary>
    public class NetworkNameState : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnNameChanged))]
        [HideInInspector]
        public FixedPlayerName Name;

        /// <summary>Fired on both server and clients whenever Name changes.</summary>
        public event Action<FixedPlayerName, FixedPlayerName> NameChanged;

        public void SetName(FixedPlayerName value) { Name = value; }

        void OnNameChanged(FixedPlayerName oldValue, FixedPlayerName newValue)
        {
            NameChanged?.Invoke(oldValue, newValue);
        }
    }

    /// <summary>
    /// Fixed-length (max 32 chars) player name wrapper with Mirror serialization support.
    /// </summary>
    [Serializable]
    public struct FixedPlayerName : IEquatable<FixedPlayerName>
    {
        const int k_MaxLength = 32;

        [SerializeField]
        string m_Name;

        public override string ToString() => m_Name ?? string.Empty;

        public static implicit operator string(FixedPlayerName s) => s.ToString();

        public static implicit operator FixedPlayerName(string s)
        {
            var name = s ?? string.Empty;
            if (name.Length > k_MaxLength)
                name = name.Substring(0, k_MaxLength);
            return new FixedPlayerName { m_Name = name };
        }

        public bool Equals(FixedPlayerName other) => string.Equals(m_Name, other.m_Name, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is FixedPlayerName other && Equals(other);

        public override int GetHashCode() => m_Name != null ? m_Name.GetHashCode() : 0;
    }

    /// <summary>
    /// Mirror NetworkReader / NetworkWriter extensions so FixedPlayerName can be used in
    /// SyncVars, Commands, ClientRpcs, and NetworkMessages.
    /// </summary>
    public static class FixedPlayerNameReaderWriterExtensions
    {
        public static void WriteFixedPlayerName(this NetworkWriter writer, FixedPlayerName value)
        {
            writer.WriteString(value.ToString());
        }

        public static FixedPlayerName ReadFixedPlayerName(this NetworkReader reader)
        {
            return (FixedPlayerName)reader.ReadString();
        }
    }
}
