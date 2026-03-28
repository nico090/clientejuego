using System.Collections.Generic;
using Mirror;
using Unity.BossRoom.Gameplay.GameplayObjects;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    public static class ActionUtils
    {
        //cache Physics Cast hits, to minimize allocs.
        static RaycastHit[] s_Hits = new RaycastHit[4];
        // cache layer IDs (after first use). -1 is a sentinel value meaning "uninitialized"
        static int s_PCLayer = -1;
        static int s_NpcLayer = -1;
        static int s_EnvironmentLayer = -1;

        /// <summary>
        /// When doing line-of-sight checks we assume the characters' "eyes" are at this height above their transform
        /// </summary>
        static readonly Vector3 k_CharacterEyelineOffset = new Vector3(0, 1, 0);

        /// <summary>
        /// When teleporting to a destination, this is how far away from the destination spot to arrive
        /// </summary>
        const float k_CloseDistanceOffset = 1;

        /// <summary>
        /// When checking if a teleport-destination is "too close" to the starting spot, anything less than this is too close
        /// </summary>
        const float k_VeryCloseTeleportRange = k_CloseDistanceOffset + 1;

        /// <summary>
        /// Detects friends and/or foes near us.
        /// </summary>
        public static int DetectNearbyEntitiesUseSphere(bool wantPcs, bool wantNpcs, Collider attacker, float range, float radius, out RaycastHit[] results)
        {
            var myBounds = attacker.bounds;

            if (s_PCLayer == -1)
                s_PCLayer = LayerMask.NameToLayer("PCs");
            if (s_NpcLayer == -1)
                s_NpcLayer = LayerMask.NameToLayer("NPCs");

            int mask = 0;
            if (wantPcs)
                mask |= (1 << s_PCLayer);
            if (wantNpcs)
                mask |= (1 << s_NpcLayer);

            int numResults = Physics.SphereCastNonAlloc(attacker.transform.position, radius,
                attacker.transform.forward, s_Hits, range, mask);

            results = s_Hits;
            return numResults;
        }

        /// <summary>
        /// Detects friends and/or foes near us.
        /// </summary>
        public static int DetectNearbyEntities(bool wantPcs, bool wantNpcs, Collider attacker, float range, out RaycastHit[] results)
        {
            var myBounds = attacker.bounds;

            if (s_PCLayer == -1)
                s_PCLayer = LayerMask.NameToLayer("PCs");
            if (s_NpcLayer == -1)
                s_NpcLayer = LayerMask.NameToLayer("NPCs");

            int mask = 0;
            if (wantPcs)
                mask |= (1 << s_PCLayer);
            if (wantNpcs)
                mask |= (1 << s_NpcLayer);

            int numResults = Physics.BoxCastNonAlloc(attacker.transform.position, myBounds.extents,
                attacker.transform.forward, s_Hits, Quaternion.identity, range, mask);

            results = s_Hits;
            return numResults;
        }

        /// <summary>
        /// Does this netId represent a valid target? Used by Target Action. The target needs to exist, be a
        /// NetworkCharacterState, and be alive.
        /// </summary>
        /// <param name="targetId">the netId of the target to investigate</param>
        /// <returns>true if this is a valid target</returns>
        public static bool IsValidTarget(uint targetId)
        {
            //note that we DON'T check if you're an ally. It's perfectly valid to target friends,
            //because there are friendly skills, such as Heal.

            if (!NetworkServer.active || !NetworkServer.spawned.TryGetValue(targetId, out var targetChar))
            {
                return false;
            }

            var targetable = targetChar.GetComponent<ITargetable>();
            return targetable != null && targetable.IsValidTarget;
        }

        /// <summary>
        /// Given the coordinates of two entities, checks to see if there is an obstacle between them.
        /// </summary>
        public static bool HasLineOfSight(Vector3 character1Pos, Vector3 character2Pos, out Vector3 missPos)
        {
            if (s_EnvironmentLayer == -1)
            {
                s_EnvironmentLayer = LayerMask.NameToLayer("Environment");
            }

            int mask = 1 << s_EnvironmentLayer;

            character1Pos += k_CharacterEyelineOffset;
            character2Pos += k_CharacterEyelineOffset;
            var rayDirection = character2Pos - character1Pos;
            var distance = rayDirection.magnitude;

            var numHits = Physics.RaycastNonAlloc(new Ray(character1Pos, rayDirection), s_Hits, distance, mask);
            if (numHits == 0)
            {
                missPos = character2Pos;
                return true;
            }
            else
            {
                missPos = s_Hits[0].point;
                return false;
            }
        }

        /// <summary>
        /// Helper method that calculates the percent a charge-up action is charged.
        /// </summary>
        public static float GetPercentChargedUp(float stoppedChargingUpTime, float timeRunning, float timeStarted, float execTime)
        {
            float timeSpentChargingUp;
            if (stoppedChargingUpTime == 0)
            {
                timeSpentChargingUp = timeRunning;
            }
            else
            {
                timeSpentChargingUp = stoppedChargingUpTime - timeStarted;
            }
            return Mathf.Clamp01(timeSpentChargingUp / execTime);
        }

        /// <summary>
        /// Determines a spot very near a chosen location for teleport destinations.
        /// </summary>
        public static Vector3 GetDashDestination(Transform characterTransform, Vector3 targetSpot, bool stopAtObstructions, float distanceToUseIfVeryClose = -1, float maxDistance = -1)
        {
            Vector3 destinationSpot = targetSpot;

            if (distanceToUseIfVeryClose != -1)
            {
                if (destinationSpot == Vector3.zero || Vector3.Distance(characterTransform.position, destinationSpot) <= k_VeryCloseTeleportRange)
                {
                    destinationSpot = characterTransform.position + characterTransform.forward * distanceToUseIfVeryClose;
                }
            }

            if (maxDistance != -1)
            {
                float distance = Vector3.Distance(characterTransform.position, destinationSpot);
                if (distance > maxDistance)
                {
                    destinationSpot = Vector3.MoveTowards(destinationSpot, characterTransform.position, distance - maxDistance);
                }
            }

            if (stopAtObstructions)
            {
                if (!HasLineOfSight(characterTransform.position, destinationSpot, out Vector3 collidePos))
                {
                    destinationSpot = collidePos;
                }
            }

            destinationSpot = Vector3.MoveTowards(destinationSpot, characterTransform.position, k_CloseDistanceOffset);

            return destinationSpot;
        }
    }

    /// <summary>
    /// Small utility to better understand action start and stop conclusion
    /// </summary>
    public static class ActionConclusion
    {
        public const bool Stop = false;
        public const bool Continue = true;
    }

    /// <summary>
    /// Utility comparer to sort through RaycastHits by distance.
    /// </summary>
    public class RaycastHitComparer : IComparer<RaycastHit>
    {
        public int Compare(RaycastHit x, RaycastHit y)
        {
            return x.distance.CompareTo(y.distance);
        }
    }
}
