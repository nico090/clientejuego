using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Pool;

namespace Unity.BossRoom.Infrastructure
{
    /// <summary>
    /// Object Pool for networked objects using Mirror.
    /// Registers custom spawn/unspawn handlers with Mirror so pooled objects are
    /// reused instead of instantiated and destroyed each time.
    ///
    /// Boss Room uses this for projectiles.
    /// </summary>
    public class NetworkObjectPool : NetworkBehaviour
    {
        public static NetworkObjectPool Singleton { get; private set; }

        [SerializeField]
        List<PoolConfigObject> PooledPrefabsList;

        HashSet<GameObject> m_Prefabs = new HashSet<GameObject>();

        Dictionary<GameObject, ObjectPool<NetworkIdentity>> m_PooledObjects =
            new Dictionary<GameObject, ObjectPool<NetworkIdentity>>();

        public void Awake()
        {
            if (Singleton != null && Singleton != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Singleton = this;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            foreach (var configObject in PooledPrefabsList)
            {
                RegisterPrefabInternal(configObject.Prefab, configObject.PrewarmCount);
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            foreach (var prefab in m_Prefabs)
            {
                NetworkClient.UnregisterSpawnHandler(prefab.GetComponent<NetworkIdentity>().assetId);
                m_PooledObjects[prefab].Clear();
            }
            m_PooledObjects.Clear();
            m_Prefabs.Clear();
        }

        public void OnValidate()
        {
            for (var i = 0; i < PooledPrefabsList.Count; i++)
            {
                var prefab = PooledPrefabsList[i].Prefab;
                if (prefab != null)
                {
                    Assert.IsNotNull(prefab.GetComponent<NetworkIdentity>(),
                        $"{nameof(NetworkObjectPool)}: Pooled prefab \"{prefab.name}\" at index {i} has no {nameof(NetworkIdentity)} component.");
                }
            }
        }

        /// <summary>Gets a pooled NetworkIdentity at the given position/rotation.</summary>
        public NetworkIdentity GetNetworkObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var identity = m_PooledObjects[prefab].Get();
            var t = identity.transform;
            t.position = position;
            t.rotation = rotation;
            return identity;
        }

        /// <summary>Returns a NetworkIdentity to the pool (disables the GameObject).</summary>
        public void ReturnNetworkObject(NetworkIdentity identity, GameObject prefab)
        {
            m_PooledObjects[prefab].Release(identity);
        }

        void RegisterPrefabInternal(GameObject prefab, int prewarmCount)
        {
            NetworkIdentity CreateFunc()
            {
                var go = Instantiate(prefab);
                // Mark pooled clones so Mirror's NetworkScenePostProcess ignores them.
                go.hideFlags = HideFlags.HideAndDontSave;
                return go.GetComponent<NetworkIdentity>();
            }

            void ActionOnGet(NetworkIdentity identity)
            {
                identity.gameObject.hideFlags = HideFlags.None;
                identity.gameObject.SetActive(true);
            }

            void ActionOnRelease(NetworkIdentity identity)
            {
                identity.gameObject.SetActive(false);
                identity.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }

            void ActionOnDestroy(NetworkIdentity identity)
            {
                if (identity != null && identity.gameObject != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(identity.gameObject);
                    else
#endif
                        Destroy(identity.gameObject);
                }
            }

            m_Prefabs.Add(prefab);

            m_PooledObjects[prefab] = new ObjectPool<NetworkIdentity>(
                CreateFunc, ActionOnGet, ActionOnRelease, ActionOnDestroy,
                defaultCapacity: prewarmCount);

            // Mirror spawn handlers: client receives the spawn message and calls spawnFunc
            NetworkClient.RegisterSpawnHandler(
                prefab.GetComponent<NetworkIdentity>().assetId,
                msg =>
                {
                    var identity = GetNetworkObject(prefab, msg.position, msg.rotation);
                    return identity.gameObject;
                },
                go => ReturnNetworkObject(go.GetComponent<NetworkIdentity>(), prefab)
            );

            // Prewarm
            var prewarm = new List<NetworkIdentity>();
            for (var i = 0; i < prewarmCount; i++)
                prewarm.Add(m_PooledObjects[prefab].Get());
            foreach (var id in prewarm)
                m_PooledObjects[prefab].Release(id);
        }
    }

    [Serializable]
    struct PoolConfigObject
    {
        public GameObject Prefab;
        public int PrewarmCount;
    }
}
