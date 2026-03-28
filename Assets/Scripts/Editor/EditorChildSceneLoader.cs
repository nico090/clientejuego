using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.BossRoom.Editor
{
    /// <summary>
    /// Editor-only utility that automatically loads child (additive) scenes in the Editor
    /// when the parent scene is opened. This is not needed at runtime because Mirror's
    /// scene management handles additive scene loading.
    /// </summary>
    [ExecuteInEditMode]
    public class EditorChildSceneLoader : MonoBehaviour
    {
        [SerializeField]
        List<string> m_ChildSceneNames = new List<string>();

        void OnEnable()
        {
            if (Application.isPlaying) return;

            foreach (var sceneName in m_ChildSceneNames)
            {
                if (string.IsNullOrEmpty(sceneName)) continue;

                // Check if scene is already loaded
                var scene = SceneManager.GetSceneByName(sceneName);
                if (scene.isLoaded) continue;

                // Find the scene asset path
                var guids = AssetDatabase.FindAssets($"t:Scene {sceneName}");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                    {
                        EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        break;
                    }
                }
            }
        }
    }
}
