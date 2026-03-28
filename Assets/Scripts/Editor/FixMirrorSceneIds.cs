using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.BossRoom.Editor
{
    /// <summary>
    /// Editor utility to fix missing Mirror sceneIds on NetworkIdentity components
    /// in the currently open scene(s). Run via Mirror > Fix Scene IDs menu.
    /// </summary>
    public static class FixMirrorSceneIds
    {
        [MenuItem("Mirror/Fix Scene IDs in Open Scenes")]
        public static void Fix()
        {
            int fixed_count = 0;

            // Find ALL NetworkIdentity components in all loaded scenes (including inactive objects)
            var identities = Resources.FindObjectsOfTypeAll<NetworkIdentity>();

            foreach (var identity in identities)
            {
                // Skip prefab assets — only process scene instances
                if (PrefabUtility.IsPartOfPrefabAsset(identity.gameObject))
                    continue;

                // Skip DontDestroyOnLoad
                if (identity.gameObject.scene.name == "DontDestroyOnLoad")
                    continue;

                // Skip objects not in a valid scene
                if (string.IsNullOrEmpty(identity.gameObject.scene.path))
                    continue;

                if (identity.sceneId == 0)
                {
                    Undo.RecordObject(identity, "Fix Mirror SceneId");
                    // Use reflection to access the private AssignSceneID or just set it directly.
                    // sceneId is a public field on NetworkIdentity.
                    identity.sceneId = GenerateSceneId();
                    EditorUtility.SetDirty(identity);
                    var scene = identity.gameObject.scene;
                    EditorSceneManager.MarkSceneDirty(scene);
                    fixed_count++;
                    Debug.Log($"[FixMirrorSceneIds] Assigned sceneId {identity.sceneId:X} to '{identity.name}' in scene '{scene.name}'");
                }
            }

            if (fixed_count > 0)
            {
                Debug.Log($"[FixMirrorSceneIds] Fixed {fixed_count} NetworkIdentity objects. SAVE YOUR SCENES NOW!");
            }
            else
            {
                Debug.Log("[FixMirrorSceneIds] All NetworkIdentity objects already have valid sceneIds.");
            }
        }

        static uint GenerateSceneId()
        {
            // Same approach Mirror uses — random uint, lower 32 bits only
            // (the upper 32 bits are the scene hash, set at build/play time)
            uint id;
            do
            {
                id = (uint)Random.Range(1, int.MaxValue);
            }
            while (id == 0);
            return id;
        }
    }
}
