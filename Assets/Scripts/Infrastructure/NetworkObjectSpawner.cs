using Mirror;
using UnityEngine;

namespace Unity.BossRoom.Infrastructure
{
    /// <summary>
    /// Utility MonoBehaviour that holds a prefab reference and can spawn it as a networked object.
    /// Attach to a GameObject and assign a prefab with a NetworkIdentity.
    /// </summary>
    public class NetworkObjectSpawner : MonoBehaviour
    {
        [SerializeField]
        GameObject prefabReference;

        /// <summary>
        /// Spawns the assigned prefab on the network at the given position and rotation.
        /// Must be called on the server.
        /// </summary>
        public static GameObject SpawnNetworkObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var go = Instantiate(prefab, position, rotation);
            NetworkServer.Spawn(go);
            return go;
        }

        /// <summary>
        /// Spawns the assigned prefab on the network with a specific owner connection.
        /// Must be called on the server.
        /// </summary>
        public static GameObject SpawnNetworkObjectAsPlayerObject(GameObject prefab, Vector3 position, Quaternion rotation, NetworkConnectionToClient owner)
        {
            var go = Instantiate(prefab, position, rotation);
            NetworkServer.Spawn(go, owner);
            return go;
        }
    }
}
