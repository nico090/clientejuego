using System.Collections.Generic;
using Mirror;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Infrastructure;
using Unity.BossRoom.Utils;
using Unity.BossRoom.VisualEffects;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
    /// <summary>
    /// Logic that handles a physics-based projectile with a collider
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class PhysicsProjectile : NetworkBehaviour
    {
        bool m_Started;

        [SerializeField]
        SphereCollider m_OurCollider;

        /// <summary>
        /// The netId of the character that created us. Can be 0 to signal that we were created generically by the server.
        /// </summary>
        uint m_SpawnerId;

        /// <summary>
        /// The data for our projectile. Indicates speed, damage, etc.
        /// </summary>
        ProjectileInfo m_ProjectileInfo;

        const int k_MaxCollisions = 4;
        const float k_WallLingerSec = 2f; //time in seconds that arrows linger after hitting a target.
        const float k_EnemyLingerSec = 0.2f; //time after hitting an enemy that we persist.
        Collider[] m_CollisionCache = new Collider[k_MaxCollisions];

        /// <summary>
        /// Time when we should destroy this arrow, in Time.time seconds.
        /// </summary>
        float m_DestroyAtSec;

        int m_CollisionMask;  //mask containing everything we test for while moving
        int m_BlockerMask;    //physics mask for things that block the arrow's flight.
        int m_NpcLayer;
        int m_PcLayer;

        /// <summary>
        /// List of everyone we've hit and dealt damage to.
        /// </summary>
        /// <remarks>
        /// Note that it's possible for entries in this list to become null if they're Destroyed post-impact.
        /// But that's fine by us! We use <c>m_HitTargets.Count</c> to tell us how many total enemies we've hit,
        /// so those nulls still count as hits.
        /// </remarks>
        List<GameObject> m_HitTargets = new List<GameObject>();

        /// <summary>
        /// Are we done moving?
        /// </summary>
        bool m_IsDead;

        [SerializeField]
        [Tooltip("Explosion prefab used when projectile hits enemy. This should have a fixed duration.")]
        SpecialFXGraphic m_OnHitParticlePrefab;

        [SerializeField]
        TrailRenderer m_TrailRenderer;

        [SerializeField]
        Transform m_Visualization;

        const float k_LerpTime = 0.1f;

        PositionLerper m_PositionLerper;

        /// <summary>
        /// Set everything up based on provided projectile information.
        /// (Note that this is called before OnStartServer/OnStartClient, so don't try to do any network stuff here.)
        /// </summary>
        public void Initialize(uint creatorsNetworkObjectId, in ProjectileInfo projectileInfo)
        {
            m_SpawnerId = creatorsNetworkObjectId;
            m_ProjectileInfo = projectileInfo;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            m_Started = true;

            m_HitTargets = new List<GameObject>();
            m_IsDead = false;

            m_DestroyAtSec = Time.fixedTime + (m_ProjectileInfo.Range / m_ProjectileInfo.Speed_m_s);

            m_CollisionMask = LayerMask.GetMask(new[] { "NPCs", "PCs", "Default", "Environment" });
            m_BlockerMask = LayerMask.GetMask(new[] { "Default", "Environment" });
            m_NpcLayer = LayerMask.NameToLayer("NPCs");
            m_PcLayer = LayerMask.NameToLayer("PCs");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            m_TrailRenderer.Clear();

            m_Visualization.parent = null;

            m_PositionLerper = new PositionLerper(transform.position, k_LerpTime);
            m_Visualization.transform.rotation = transform.rotation;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            m_Started = false;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            m_TrailRenderer.Clear();
            m_Visualization.parent = transform;
        }

        void OnDestroy()
        {
            // Safety: if the visualization was detached and OnStopClient didn't run
            // (e.g. scene change), destroy the orphaned visual object.
            if (m_Visualization != null && m_Visualization.parent == null)
            {
                Destroy(m_Visualization.gameObject);
            }
        }

        void FixedUpdate()
        {
            if (!m_Started || !isServer)
            {
                return; //don't do anything before OnStartServer has run.
            }

            if (m_DestroyAtSec < Time.fixedTime)
            {
                // Time to return to the pool from whence it came.
                NetworkServer.UnSpawn(gameObject);
                return;
            }

            var displacement = transform.forward * (m_ProjectileInfo.Speed_m_s * Time.fixedDeltaTime);
            transform.position += displacement;

            if (!m_IsDead)
            {
                DetectCollisions();
            }
        }

        void Update()
        {
            if (isClient)
            {
                // One thing to note: this graphics GameObject is detached from its parent on OnStartClient. On the host,
                // the m_Parent Transform is translated via PhysicsProjectile's FixedUpdate method. On all other
                // clients, m_Parent's NetworkTransform handles syncing and interpolating the m_Parent Transform. Thus, to
                // eliminate any visual jitter on the host, this GameObject is positionally smoothed over time. On all other
                // clients, no positional smoothing is required, since m_Parent's NetworkTransform will perform
                // positional interpolation on its Update method, and so this position is simply matched 1:1 with m_Parent.

                if (isServer && isClient) // host
                {
                    m_Visualization.position = m_PositionLerper.LerpPosition(m_Visualization.position,
                        transform.position);
                }
                else
                {
                    m_Visualization.position = transform.position;
                }
            }
        }

        void DetectCollisions()
        {
            var position = transform.localToWorldMatrix.MultiplyPoint(m_OurCollider.center);
            var numCollisions = Physics.OverlapSphereNonAlloc(position, m_OurCollider.radius, m_CollisionCache, m_CollisionMask);
            for (int i = 0; i < numCollisions; i++)
            {
                int layerTest = 1 << m_CollisionCache[i].gameObject.layer;
                if ((layerTest & m_BlockerMask) != 0)
                {
                    //hit a wall; leave it for a couple of seconds.
                    m_ProjectileInfo.Speed_m_s = 0;
                    m_IsDead = true;
                    m_DestroyAtSec = Time.fixedTime + k_WallLingerSec;
                    return;
                }

                // PvP: projectiles can hit both NPCs and other players (but not the spawner)
                int layer = m_CollisionCache[i].gameObject.layer;
                if ((layer == m_NpcLayer || layer == m_PcLayer) && !m_HitTargets.Contains(m_CollisionCache[i].gameObject))
                {
                    var targetNetObj = m_CollisionCache[i].GetComponentInParent<NetworkIdentity>();
                    if (targetNetObj && targetNetObj.netId == m_SpawnerId)
                    {
                        continue; // don't hit ourselves
                    }

                    m_HitTargets.Add(m_CollisionCache[i].gameObject);

                    if (m_HitTargets.Count >= m_ProjectileInfo.MaxVictims)
                    {
                        m_DestroyAtSec = Time.fixedTime + k_EnemyLingerSec;
                        m_IsDead = true;
                    }

                    if (targetNetObj)
                    {
                        ClientHitEnemyRpc(targetNetObj.netId);

                        NetworkServer.spawned.TryGetValue(m_SpawnerId, out var spawnerIdentity);
                        var spawnerObj = spawnerIdentity != null ? spawnerIdentity.GetComponent<ServerCharacter>() : null;

                        if (m_CollisionCache[i].TryGetComponent(out IDamageable damageable))
                        {
                            damageable.ReceiveHitPoints(spawnerObj, -m_ProjectileInfo.Damage);
                        }
                    }

                    if (m_IsDead)
                    {
                        return;
                    }
                }
            }
        }

        [ClientRpc]
        void ClientHitEnemyRpc(uint enemyId)
        {
            //in the future we could do quite fancy things, like deparenting the Graphics Arrow and parenting it to the target.
            //For the moment we play some particles (optionally), and cause the target to animate a hit-react.

            if (NetworkClient.spawned.TryGetValue(enemyId, out var targetNetObject))
            {
                if (m_OnHitParticlePrefab)
                {
                    // show an impact graphic
                    Instantiate(m_OnHitParticlePrefab.gameObject, transform.position, transform.rotation);
                }
            }
        }
    }
}
