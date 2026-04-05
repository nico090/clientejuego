using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Unity.BossRoom.Infrastructure
{
    /// <summary>
    /// Centralized dispatcher for BossRoomChannelMessage. Routes incoming messages
    /// to the correct NetworkedMessageChannel by channel name.
    /// This solves the problem of ReplaceHandler overwriting previous handlers
    /// when multiple NetworkedMessageChannel instances exist.
    /// </summary>
    static class NetworkedChannelDispatcher
    {
        static readonly Dictionary<string, Action<BossRoomChannelMessage>> s_Handlers =
            new Dictionary<string, Action<BossRoomChannelMessage>>();

        static bool s_Registered;

        public static void Register(string channelName, Action<BossRoomChannelMessage> handler)
        {
            s_Handlers[channelName] = handler;
            EnsureHandlerRegistered();
        }

        public static void Unregister(string channelName)
        {
            s_Handlers.Remove(channelName);
        }

        static void EnsureHandlerRegistered()
        {
            if (s_Registered) return;
            s_Registered = true;
            NetworkClient.ReplaceHandler<BossRoomChannelMessage>(OnMessage, requireAuthentication: false);
        }

        static void OnMessage(BossRoomChannelMessage msg)
        {
            if (s_Handlers.TryGetValue(msg.ChannelName, out var handler))
            {
                handler.Invoke(msg);
            }
        }
    }

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
            NetworkedChannelDispatcher.Register(m_ChannelName, ReceiveMessageThroughNetwork);
        }

        public override void Dispose()
        {
            if (!IsDisposed)
            {
                NetworkedChannelDispatcher.Unregister(m_ChannelName);
            }
            base.Dispose();
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
