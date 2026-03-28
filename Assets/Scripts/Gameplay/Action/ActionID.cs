using System;
using Mirror;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// This struct is used by Action system (and GameDataSource) to refer to a specific action in runtime.
    /// It wraps a simple integer.
    /// </summary>
    public struct ActionID : IEquatable<ActionID>
    {
        public int ID;

        public bool Equals(ActionID other)
        {
            return ID == other.ID;
        }

        public override bool Equals(object obj)
        {
            return obj is ActionID other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ID;
        }

        public static bool operator ==(ActionID x, ActionID y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(ActionID x, ActionID y)
        {
            return !(x == y);
        }

        public override string ToString()
        {
            return $"ActionID({ID})";
        }
    }

    /// <summary>
    /// Mirror NetworkWriter/NetworkReader extensions so ActionID can be used in
    /// Commands, ClientRpcs, and NetworkMessages.
    /// </summary>
    public static class ActionIDReaderWriterExtensions
    {
        public static void WriteActionID(this NetworkWriter writer, ActionID value)
        {
            writer.WriteInt(value.ID);
        }

        public static ActionID ReadActionID(this NetworkReader reader)
        {
            return new ActionID { ID = reader.ReadInt() };
        }
    }
}
