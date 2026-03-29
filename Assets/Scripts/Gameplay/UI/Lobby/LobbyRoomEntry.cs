using System;
using TMPro;
using Unity.BossRoom.ConnectionManagement.Lobby;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.BossRoom.Gameplay.UI
{
    /// <summary>
    /// UI component for a single room entry in the lobby list.
    /// Attach to the RoomEntry prefab.
    /// </summary>
    public class LobbyRoomEntry : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI m_RoomNameText;
        [SerializeField] TextMeshProUGUI m_PlayerCountText;
        [SerializeField] TextMeshProUGUI m_StatusText;
        [SerializeField] GameObject m_LockIcon;
        [SerializeField] Button m_JoinButton;

        RoomInfo m_RoomInfo;
        Action<RoomInfo> m_OnJoinClicked;

        public void Setup(RoomInfo roomInfo, Action<RoomInfo> onJoinClicked)
        {
            m_RoomInfo = roomInfo;
            m_OnJoinClicked = onJoinClicked;

            m_RoomNameText.text = roomInfo.name;
            m_PlayerCountText.text = $"{roomInfo.current_players}/{roomInfo.max_players}";
            m_LockIcon.SetActive(roomInfo.has_password);

            switch (roomInfo.status)
            {
                case "starting":
                    m_StatusText.text = "Starting...";
                    m_StatusText.color = new Color(1f, 0.8f, 0.2f); // yellow
                    break;
                case "ready":
                    m_StatusText.text = "Ready";
                    m_StatusText.color = new Color(0.3f, 1f, 0.3f); // green
                    break;
                case "in_game":
                    m_StatusText.text = "In Game";
                    m_StatusText.color = new Color(0.5f, 0.7f, 1f); // light blue
                    break;
                default:
                    m_StatusText.text = roomInfo.status;
                    m_StatusText.color = Color.white;
                    break;
            }

            bool canJoin = roomInfo.status == "ready" && roomInfo.current_players < roomInfo.max_players;
            m_JoinButton.interactable = canJoin;
            m_JoinButton.onClick.AddListener(OnJoinButtonClicked);
        }

        void OnJoinButtonClicked()
        {
            m_OnJoinClicked?.Invoke(m_RoomInfo);
        }

        void OnDestroy()
        {
            m_JoinButton.onClick.RemoveListener(OnJoinButtonClicked);
        }
    }
}
