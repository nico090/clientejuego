using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.BossRoom.Utils
{
    /// This utility helps showing Network statistics at runtime.
    ///
    /// This component attaches to any networked object.
    /// It'll spawn all the needed text and canvas.
    ///
    /// NOTE: This class will be removed once Unity provides support for this.
    [RequireComponent(typeof(NetworkIdentity))]
    public class NetworkStats : NetworkBehaviour
    {
        // For a value like RTT an exponential moving average is a better indication of the current rtt
        struct ExponentialMovingAverageCalculator
        {
            readonly float m_Alpha;
            float m_Average;

            public float Average => m_Average;

            public ExponentialMovingAverageCalculator(float average)
            {
                m_Alpha = 2f / (k_MaxWindowSize + 1);
                m_Average = average;
            }

            public float NextValue(float value) => m_Average = (value - m_Average) * m_Alpha + m_Average;
        }

        // RTT is measured via a ping/pong Command+ClientRpc round trip.
        const int k_MaxWindowSizeSeconds = 3;
        const float k_PingIntervalSeconds = 0.1f;
        const float k_MaxWindowSize = k_MaxWindowSizeSeconds / k_PingIntervalSeconds;

        const float k_StrugglingNetworkConditionsRTTThreshold = 130;
        const float k_BadNetworkConditionsRTTThreshold = 200;

        ExponentialMovingAverageCalculator m_BossRoomRTT = new ExponentialMovingAverageCalculator(0);
        ExponentialMovingAverageCalculator m_TransportRTT = new ExponentialMovingAverageCalculator(0);

        float m_LastPingTime;
        TextMeshProUGUI m_TextStat;
        TextMeshProUGUI m_TextHostType;
        TextMeshProUGUI m_TextBadNetworkConditions;

        int m_CurrentRTTPingId;
        Dictionary<int, float> m_PingHistoryStartTimes = new Dictionary<int, float>();

        string m_TextToDisplay;

        public override void OnStartClient()
        {
            base.OnStartClient();

            bool isClientOnly = isClient && !isServer;
            if (!isLocalPlayer && isClientOnly)
            {
                enabled = false;
                return;
            }

            if (isLocalPlayer)
            {
                CreateNetworkStatsText();
            }
        }

        // Creating a UI text object and add it to NetworkOverlay canvas
        void CreateNetworkStatsText()
        {
            Assert.IsNotNull(Editor.NetworkOverlay.Instance,
                "No NetworkOverlay object part of scene. Add NetworkOverlay prefab to bootstrap scene!");

            string hostType = isServer && isClient ? "Host" : isClient ? "Client" : "Unknown";
            Editor.NetworkOverlay.Instance.AddTextToUI("UI Host Type Text", $"Type: {hostType}", out m_TextHostType);
            Editor.NetworkOverlay.Instance.AddTextToUI("UI Stat Text", "No Stat", out m_TextStat);
            Editor.NetworkOverlay.Instance.AddTextToUI("UI Bad Conditions Text", "", out m_TextBadNetworkConditions);
        }

        void FixedUpdate()
        {
            if (!isServer)
            {
                if (Time.realtimeSinceStartup - m_LastPingTime > k_PingIntervalSeconds)
                {
                    CmdPing(m_CurrentRTTPingId);
                    m_PingHistoryStartTimes[m_CurrentRTTPingId] = Time.realtimeSinceStartup;
                    m_CurrentRTTPingId++;
                    m_LastPingTime = Time.realtimeSinceStartup;

                    // Mirror's NetworkTime provides RTT info
                    m_TransportRTT.NextValue((float)NetworkTime.rtt * 1000f);
                }

                if (m_TextStat != null)
                {
                    m_TextToDisplay = $"RTT: {(m_BossRoomRTT.Average * 1000).ToString("0")} ms;\nTransport RTT {m_TransportRTT.Average.ToString("0")} ms";
                    if (m_TransportRTT.Average > k_BadNetworkConditionsRTTThreshold)
                    {
                        m_TextStat.color = Color.red;
                    }
                    else if (m_TransportRTT.Average > k_StrugglingNetworkConditionsRTTThreshold)
                    {
                        m_TextStat.color = Color.yellow;
                    }
                    else
                    {
                        m_TextStat.color = Color.white;
                    }
                }

                if (m_TextBadNetworkConditions != null)
                {
                    m_TextBadNetworkConditions.text = m_TransportRTT.Average > k_BadNetworkConditionsRTTThreshold
                        ? "Bad Network Conditions Detected!"
                        : "";
                    var color = Color.red;
                    color.a = Mathf.PingPong(Time.time, 1f);
                    m_TextBadNetworkConditions.color = color;
                }
            }
            else
            {
                m_TextToDisplay = $"Connected players: {NetworkServer.connections.Count}";
            }

            if (m_TextStat)
            {
                m_TextStat.text = m_TextToDisplay;
            }
        }

        [Command]
        void CmdPing(int pingId, NetworkConnectionToClient sender = null)
        {
            // sender is auto-populated by Mirror with the connection that sent the command
            RpcPong(sender, pingId);
        }

        /// <summary>
        /// TargetRpc — Mirror routes this to the specific client connection.
        /// The first NetworkConnection parameter is consumed by Mirror and not received as data.
        /// </summary>
        [TargetRpc]
        void RpcPong(NetworkConnection target, int pingId)
        {
            if (m_PingHistoryStartTimes.TryGetValue(pingId, out var startTime))
            {
                m_PingHistoryStartTimes.Remove(pingId);
                m_BossRoomRTT.NextValue(Time.realtimeSinceStartup - startTime);
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (m_TextStat != null)
            {
                Destroy(m_TextStat.gameObject);
            }

            if (m_TextHostType != null)
            {
                Destroy(m_TextHostType.gameObject);
            }

            if (m_TextBadNetworkConditions != null)
            {
                Destroy(m_TextBadNetworkConditions.gameObject);
            }
        }
    }
}
