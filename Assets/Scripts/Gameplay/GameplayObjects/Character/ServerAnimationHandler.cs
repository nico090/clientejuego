using System;
using Mirror;
using Unity.BossRoom.Gameplay.Configuration;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    public class ServerAnimationHandler : NetworkBehaviour
    {
        [SerializeField]
        NetworkAnimator m_NetworkAnimator;

        [SerializeField]
        VisualizationConfiguration m_VisualizationConfiguration;

        [SerializeField]
        NetworkLifeState m_NetworkLifeState;

        public NetworkAnimator NetworkAnimator => m_NetworkAnimator;

        public override void OnStartServer()
        {
            base.OnStartServer();
            m_NetworkLifeState.LifeStateChanged += OnLifeStateChanged;
        }

        void OnLifeStateChanged(LifeState previousValue, LifeState newValue)
        {
            switch (newValue)
            {
                case LifeState.Alive:
                    NetworkAnimator.SetTrigger(m_VisualizationConfiguration.AliveStateTriggerID);
                    break;
                case LifeState.Fainted:
                    NetworkAnimator.SetTrigger(m_VisualizationConfiguration.FaintedStateTriggerID);
                    break;
                case LifeState.Dead:
                    NetworkAnimator.SetTrigger(m_VisualizationConfiguration.DeadStateTriggerID);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(newValue), newValue, null);
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (m_NetworkLifeState != null)
            {
                m_NetworkLifeState.LifeStateChanged -= OnLifeStateChanged;
            }
        }
    }
}
