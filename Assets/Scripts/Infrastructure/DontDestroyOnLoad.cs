using UnityEngine;

namespace Unity.BossRoom.Infrastructure
{
    /// <summary>
    /// Simple utility to mark a GameObject as DontDestroyOnLoad on Awake.
    /// </summary>
    public class DontDestroyOnLoad : MonoBehaviour
    {
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
