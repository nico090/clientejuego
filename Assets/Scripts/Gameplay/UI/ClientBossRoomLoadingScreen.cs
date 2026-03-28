using Unity.BossRoom.Gameplay.GameplayObjects;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.UI
{
    /// <summary>
    /// Loading screen that shows player names during scene transitions.
    /// The NGO-based loading progress tracking is not available with Mirror;
    /// Mirror handles scene loading via NetworkManager.ServerChangeScene.
    /// </summary>
    public class ClientBossRoomLoadingScreen : MonoBehaviour
    {
        [SerializeField]
        PersistentPlayerRuntimeCollection m_PersistentPlayerRuntimeCollection;

        string GetPlayerName(ulong clientId)
        {
            foreach (var player in m_PersistentPlayerRuntimeCollection.Items)
            {
                if (clientId == player.OwnerConnectionId)
                {
                    return player.NetworkNameState.Name;
                }
            }
            return "";
        }
    }
}
