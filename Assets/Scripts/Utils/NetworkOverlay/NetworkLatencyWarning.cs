// Unity.Multiplayer.Tools.NetworkSimulator.Runtime requires NGO (UNITY_NETCODE_GAMEOBJECTS_1_1_ABOVE).
// Since this project uses Mirror, that assembly is unavailable.
// Latency detection is disabled; the overlay text is never shown.
using Mirror;
using TMPro;
using UnityEngine;

namespace Unity.BossRoom.Utils.Editor
{
    public class NetworkLatencyWarning : MonoBehaviour
    {
        TextMeshProUGUI m_LatencyText;
        bool m_LatencyTextCreated;

        void Update()
        {
            // NetworkSimulator from com.unity.multiplayer.tools requires NGO.
            // Artificial latency detection is not available with Mirror transport.
            if (m_LatencyTextCreated)
            {
                m_LatencyTextCreated = false;
                Destroy(m_LatencyText);
            }
        }
    }
}
