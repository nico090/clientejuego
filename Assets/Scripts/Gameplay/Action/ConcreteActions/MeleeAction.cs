using System;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// Action that represents a swing of a melee weapon. It is not explicitly targeted, but rather detects the foe that was hit with a physics check.
    /// </summary>
    [CreateAssetMenu(menuName = "BossRoom/Actions/Melee Action")]
    public partial class MeleeAction : Action
    {
        private bool m_ExecutionFired;
        private uint m_ProvisionalTarget;

        public override bool OnStart(ServerCharacter serverCharacter)
        {
            uint target = (Data.TargetIds != null && Data.TargetIds.Length > 0) ? Data.TargetIds[0] : serverCharacter.TargetId;
            IDamageable foe = DetectFoe(serverCharacter, target);
            if (foe != null)
            {
                m_ProvisionalTarget = foe.netId;
                Data.TargetIds = new uint[] { foe.netId };
            }

            // snap to face the right direction
            if (Data.Direction != Vector3.zero)
            {
                serverCharacter.physicsWrapper.Transform.forward = Data.Direction;
            }

            serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);
            serverCharacter.ClientPlayActionRpc(Data);
            return true;
        }

        public override void Reset()
        {
            base.Reset();
            m_ExecutionFired = false;
            m_ProvisionalTarget = 0;
            m_ImpactPlayed = false;
            m_SpawnedGraphics = null;
        }

        public override bool OnUpdate(ServerCharacter clientCharacter)
        {
            if (!m_ExecutionFired && (Time.time - TimeStarted) >= Config.ExecTimeSeconds)
            {
                m_ExecutionFired = true;
                var foe = DetectFoe(clientCharacter, m_ProvisionalTarget);
                if (foe != null)
                {
                    foe.ReceiveHitPoints(clientCharacter, -Config.Amount);
                }
            }

            return true;
        }

        /// <summary>
        /// Returns the IDamageable of the foe we hit, or null if none found.
        /// </summary>
        private IDamageable DetectFoe(ServerCharacter parent, uint foeHint = 0)
        {
            return GetIdealMeleeFoe(Config.IsFriendly ^ parent.IsNpc, parent.physicsWrapper.DamageCollider, Config.Range, Config.Radius, foeHint, parent.netId);
        }

        /// <summary>
        /// Utility used by Actions to perform Melee attacks.
        /// In PvP mode, players can hit both NPCs and other players.
        /// </summary>
        public static IDamageable GetIdealMeleeFoe(bool isNPC, Collider ourCollider, float meleeRange, float meleeRadius, uint preferredTargetNetworkId, uint selfNetId = 0)
        {
            // PvP: search both PCs and NPCs for all attackers
            RaycastHit[] results;
            int numResults = 0.0f < meleeRadius
                ? ActionUtils.DetectNearbyEntitiesUseSphere(true, true, ourCollider, meleeRange, meleeRadius, out results)
                : ActionUtils.DetectNearbyEntities(true, true, ourCollider, meleeRange, out results);

            IDamageable foundFoe = null;

            int maxDamage = int.MinValue;

            for (int i = 0; i < numResults; i++)
            {
                var damageable = results[i].collider.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsDamageable())
                {
                    continue;
                }

                // skip self
                if (damageable.netId == selfNetId)
                {
                    continue;
                }

                if (damageable.netId == preferredTargetNetworkId)
                {
                    foundFoe = damageable;
                    maxDamage = int.MaxValue;
                    continue;
                }

                var totalDamage = damageable.GetTotalDamage();
                if (foundFoe == null || maxDamage < totalDamage)
                {
                    foundFoe = damageable;
                    maxDamage = totalDamage;
                }
            }

            return foundFoe;
        }
    }
}
