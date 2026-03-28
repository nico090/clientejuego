using System;
using Unity.BossRoom.Audio;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Audio
{
    /// <summary>
    /// Simple class to restart game theme on main menu load
    /// </summary>
    [RequireComponent(typeof(NetworkLifeState)), RequireComponent(typeof(NetworkHealthState))]
    public class BossMusicStarter : MonoBehaviour
    {
        [SerializeField]
        NetworkLifeState m_NetworkLifeState;

        [SerializeField]
        NetworkHealthState m_NetworkHealthState;

        bool m_Won;

        void Start()
        {
            Assert.IsNotNull(m_NetworkLifeState, "NetworkLifeState not set!");
            Assert.IsNotNull(m_NetworkHealthState, "NetworkHealthState not set!");

            m_NetworkLifeState.LifeStateChanged += OnLifeStateChanged;
            m_NetworkHealthState.HitPointsChanged += OnHealthChanged;
        }

        void OnDestroy()
        {
            if (m_NetworkLifeState != null)
            {
                m_NetworkLifeState.LifeStateChanged -= OnLifeStateChanged;
            }
            if (m_NetworkHealthState != null)
            {
                m_NetworkHealthState.HitPointsChanged -= OnHealthChanged;
            }
        }

        private void OnLifeStateChanged(LifeState previousValue, LifeState newValue)
        {
            if (newValue != LifeState.Alive)
            {
                // players won! Start victory theme
                ClientMusicPlayer.Instance.PlayVictoryMusic();
                m_Won = true;
            }
        }

        private void OnHealthChanged(int previousValue, int newValue)
        {
            // don't do anything if battle is over
            if (m_Won) { return; }

            // make sure battle music started anytime boss is hurt
            if (newValue < previousValue)
            {
                ClientMusicPlayer.Instance.PlayBossMusic();
            }
        }
    }
}
