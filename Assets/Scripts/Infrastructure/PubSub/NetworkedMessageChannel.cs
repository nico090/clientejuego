using Mirror;
using UnityEngine;

namespace Unity.BossRoom.Infrastructure
{
    /// <summary>
    /// Mirror-based networked message channel. The server publishes a message that is sent to all clients
    /// and also published locally. Clients subscribe to receive messages from the server.
    ///
    /// Uses JsonUtility for serialization so T can be any serializable struct (not limited to unmanaged).
    /// </summary>
    public class NetworkedMessageChannel<T> : MessageChannel<T> where T : struct
    {
        readonly string m_ChannelName;

        public NetworkedMessageChannel()
        {
            m_ChannelName = typeof(T).FullName;

            // Always register immediately. Mirror's RegisterMessageHandlers does not
            // clear custom handlers, so this survives ConnectHost/scene changes.
            // NOTE: We cannot use NetworkClient.OnConnectedEvent because Mirror's
            // NetworkManager overwrites it with `= OnClientConnectInternal` (not +=).
            RegisterHandler();
        }

        public override void Dispose()
        {
            if (!IsDisposed)
            {
                NetworkClient.UnregisterHandler<BossRoomChannelMessage>();
            }
            base.Dispose();
        }

        void RegisterHandler()
        {
            // Always register so the local host client can handle the message too.
            // ReplaceHandler avoids "replacing handler" warnings when multiple channels exist.
            NetworkClient.ReplaceHandler<BossRoomChannelMessage>(ReceiveMessageThroughNetwork, requireAuthentication: false);
        }

        public override void Publish(T message)
        {
            if (NetworkServer.active)
            {
                SendMessageThroughNetwork(message);
                base.Publish(message);
            }
            else
            {
                Debug.LogError("Only a server can publish in a NetworkedMessageChannel");
            }
        }

        void SendMessageThroughNetwork(T message)
        {
            NetworkServer.SendToAll(new BossRoomChannelMessage
            {
                ChannelName = m_ChannelName,
                Data = JsonUtility.ToJson(message)
            });
        }

        void ReceiveMessageThroughNetwork(BossRoomChannelMessage msg)
        {
            // In host mode, Publish() already called base.Publish locally — skip to avoid duplicates.
            if (NetworkServer.active) return;

            if (msg.ChannelName != m_ChannelName)
                return;

            var message = JsonUtility.FromJson<T>(msg.Data);
            base.Publish(message);
        }
    }

    /// <summary>
    /// Generic network message wrapper used by NetworkedMessageChannel.
    /// </summary>
    public struct BossRoomChannelMessage : NetworkMessage
    {
        public string ChannelName;
        public string Data;
    }
}
