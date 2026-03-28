using TMPro;
using Unity.BossRoom.Utils;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.UI
{
    /// <summary>
    /// UI object that visually represents an object's name. Visuals are updated via
    /// NetworkNameState events (Mirror SyncVar hooks).
    /// </summary>
    public class UIName : MonoBehaviour
    {
        [SerializeField]
        TextMeshProUGUI m_UINameText;

        NetworkNameState m_NetworkNameState;

        public void Initialize(NetworkNameState networkNameState)
        {
            m_NetworkNameState = networkNameState;

            m_UINameText.text = networkNameState.Name.ToString();
            networkNameState.NameChanged += NameUpdated;
        }

        void NameUpdated(FixedPlayerName previousValue, FixedPlayerName newValue)
        {
            m_UINameText.text = newValue.ToString();
        }

        void OnDestroy()
        {
            if (m_NetworkNameState != null)
            {
                m_NetworkNameState.NameChanged -= NameUpdated;
            }
        }
    }
}
