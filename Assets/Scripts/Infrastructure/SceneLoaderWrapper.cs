using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.BossRoom.Infrastructure
{
    public class SceneLoaderWrapper : MonoBehaviour
    {
        private static SceneLoaderWrapper s_Instance;

        public static SceneLoaderWrapper Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindObjectOfType<SceneLoaderWrapper>();
                    if (s_Instance == null)
                    {
                        var go = new GameObject("SceneLoaderWrapper");
                        s_Instance = go.AddComponent<SceneLoaderWrapper>();
                    }
                }
                return s_Instance;
            }
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void LoadScene(string sceneName, bool useNetworkSceneManager)
        {
            if (useNetworkSceneManager && NetworkManager.singleton != null)
            {
                NetworkManager.singleton.ServerChangeScene(sceneName);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        public void HandleServerSceneChanged(string sceneName) { }
        public void HandleClientSceneChanged(string sceneName) { }
    }
}