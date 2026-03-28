using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.Animations;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
    /// <summary>
    /// Component to simply play a descending animation when this NetworkIdentity's parent changes.
    /// </summary>
    public class ServerDisplacerOnParentChange : NetworkBehaviour
    {
        [SerializeField]
        NetworkTransformReliable m_NetworkTransformReliable;

        [SerializeField]
        PositionConstraint m_PositionConstraint;

        const float k_DropAnimationLength = 0.1f;

        void Awake()
        {
            m_PositionConstraint.enabled = false;
            enabled = false;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            m_PositionConstraint.enabled = true;
            enabled = true;
        }

        /// <summary>
        /// Called by Unity when this transform's parent changes. Mirror does not have a built-in
        /// OnNetworkObjectParentChanged callback, so we use the MonoBehaviour equivalent.
        /// </summary>
        void OnTransformParentChanged()
        {
            if (!isServer)
            {
                return;
            }

            RemoveParentConstraintSources();

            if (transform.parent == null)
            {
                StopAllCoroutines();

                // when the object is unparented, sync in world space and smooth-drop it to the ground
                m_NetworkTransformReliable.enabled = true;
                m_PositionConstraint.enabled = true;

                StartCoroutine(SmoothPositionLerpY(k_DropAnimationLength, 0));
            }
            // when parented, NetworkTransformReliable syncs in local space automatically if configured to do so
        }

        void RemoveParentConstraintSources()
        {
            if (m_PositionConstraint)
            {
                for (int i = m_PositionConstraint.sourceCount - 1; i >= 0; i--)
                {
                    m_PositionConstraint.RemoveSource(i);
                }
            }
        }

        IEnumerator SmoothPositionLerpY(float length, float targetHeight)
        {
            var start = transform.position.y;

            var progress = 0f;
            var duration = 0f;

            while (progress < 1f)
            {
                duration += Time.deltaTime;
                progress = Mathf.Clamp(duration / length, 0f, 1f);
                var progressY = Mathf.Lerp(start, targetHeight, progress);

                transform.position = new Vector3(transform.position.x, progressY, transform.position.z);

                yield return null;
            }
        }
    }
}
