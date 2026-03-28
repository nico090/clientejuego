using System;
using Mirror;

namespace Unity.BossRoom.Infrastructure
{
    /// <summary>
    /// A GUID split into two ulongs for network serialization with Mirror.
    /// </summary>
    public struct NetworkGuid
    {
        public ulong FirstHalf;
        public ulong SecondHalf;
    }

    public static class NetworkGuidExtensions
    {
        public static NetworkGuid ToNetworkGuid(this Guid id)
        {
            var networkId = new NetworkGuid();
            networkId.FirstHalf = BitConverter.ToUInt64(id.ToByteArray(), 0);
            networkId.SecondHalf = BitConverter.ToUInt64(id.ToByteArray(), 8);
            return networkId;
        }

        public static Guid ToGuid(this NetworkGuid networkId)
        {
            var bytes = new byte[16];
            Buffer.BlockCopy(BitConverter.GetBytes(networkId.FirstHalf), 0, bytes, 0, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(networkId.SecondHalf), 0, bytes, 8, 8);
            return new Guid(bytes);
        }
    }

    /// <summary>
    /// Mirror NetworkReader / NetworkWriter extensions so NetworkGuid can be
    /// serialized inside SyncVars, Commands, ClientRpcs, and NetworkMessages.
    /// </summary>
    public static class NetworkGuidReaderWriterExtensions
    {
        public static void WriteNetworkGuid(this NetworkWriter writer, NetworkGuid value)
        {
            writer.WriteULong(value.FirstHalf);
            writer.WriteULong(value.SecondHalf);
        }

        public static NetworkGuid ReadNetworkGuid(this NetworkReader reader)
        {
            return new NetworkGuid
            {
                FirstHalf = reader.ReadULong(),
                SecondHalf = reader.ReadULong()
            };
        }
    }
}
