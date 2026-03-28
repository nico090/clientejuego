using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.BossRoom.Infrastructure
{
    /// <summary>
    /// Server-side component that loads an additive scene when a player enters a trigger
    /// and unloads it (after a delay) when all players leave.
    /// Uses Mirror's NetworkServer scene management.
    /// </summary>
    public class ServerAdditiveSceneLoader : NetworkBehaviour
    {
        [SerializeField]
        float m_DelayBeforeUnload = 5f;

        [SerializeField]
        string m_SceneName;

        [SerializeField]
        string m_PlayerTag = "Player";

        int m_PlayersInTrigger;
        bool m_IsSceneLoaded;
        Coroutine m_UnloadCoroutine;

        void OnTriggerEnter(Collider other)
        {
            if (!isServer) return;
            if (!other.CompareTag(m_PlayerTag)) return;

            m_PlayersInTrigger++;

            if (!m_IsSceneLoaded && !string.IsNullOrEmpty(m_SceneName))
            {
                if (m_UnloadCoroutine != null)
                {
                    StopCoroutine(m_UnloadCoroutine);
                    m_UnloadCoroutine = null;
                }
                StartCoroutine(LoadAdditiveScene());
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (!isServer) return;
            if (!other.CompareTag(m_PlayerTag)) return;

            m_PlayersInTrigger--;

            if (m_PlayersInTrigger <= 0 && m_IsSceneLoaded)
            {
                m_PlayersInTrigger = 0;
                m_UnloadCoroutine = StartCoroutine(UnloadAdditiveSceneWithDelay());
            }
        }

        IEnumerator LoadAdditiveScene()
        {
            if (m_IsSceneLoaded) yield break;

            var asyncOp = SceneManager.LoadSceneAsync(m_SceneName, LoadSceneMode.Additive);
            if (asyncOp == null) yield break;

            while (!asyncOp.isDone)
                yield return null;

            m_IsSceneLoaded = true;
        }

        IEnumerator UnloadAdditiveSceneWithDelay()
        {
            yield return new WaitForSeconds(m_DelayBeforeUnload);

            if (m_PlayersInTrigger > 0)
            {
                m_UnloadCoroutine = null;
                yield break;
            }

            var scene = SceneManager.GetSceneByName(m_SceneName);
            if (scene.isLoaded)
            {
                var asyncOp = SceneManager.UnloadSceneAsync(scene);
                if (asyncOp != null)
                {
                    while (!asyncOp.isDone)
                        yield return null;
                }
            }

            m_IsSceneLoaded = false;
            m_UnloadCoroutine = null;
        }
    }
}
