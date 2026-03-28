// Unity.Multiplayer.Tools.NetworkSimulator.Runtime requires NGO (UNITY_NETCODE_GAMEOBJECTS_1_1_ABOVE).
// Since this project uses Mirror, that assembly is unavailable.
// The NetworkSimulator UI is disabled; the canvas show/hide logic based on Mirror is kept.
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.BossRoom.Utils
{
    public class NetworkSimulatorUIMediator : MonoBehaviour
    {
        [SerializeField]
        CanvasGroup m_CanvasGroup;

        [SerializeField]
        InputActionReference m_ToggleNetworkSimulatorAction;

        bool m_Shown;

        void Awake()
        {
            Hide();
        }

        void Start()
        {
            NetworkClient.OnConnectedEvent += Show;
            if (m_ToggleNetworkSimulatorAction != null)
                m_ToggleNetworkSimulatorAction.action.performed += OnToggle;
        }

        void OnDestroy()
        {
            NetworkClient.OnConnectedEvent -= Show;
            if (m_ToggleNetworkSimulatorAction != null)
                m_ToggleNetworkSimulatorAction.action.performed -= OnToggle;
        }

        public void Hide()
        {
            m_CanvasGroup.alpha = 0f;
            m_CanvasGroup.interactable = false;
            m_CanvasGroup.blocksRaycasts = false;
            m_Shown = false;
        }

        void Show()
        {
            m_CanvasGroup.alpha = 1f;
            m_CanvasGroup.interactable = true;
            m_CanvasGroup.blocksRaycasts = true;
            m_Shown = true;
        }

        void OnToggle(InputAction.CallbackContext ctx)
        {
            if (m_Shown) Hide(); else Show();
        }

        // Stubs kept so existing scene references don't break
        public void SimulateDisconnect() { }
        public void TriggerLagSpike() { }
        public void SanitizeLagSpikeDurationInputField() { }
        public void TriggerScenario() { }
    }
}
