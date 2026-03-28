using Mirror;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Gameplay.GameState;
using Unity.BossRoom.Gameplay.Messages;
using Unity.BossRoom.Infrastructure;
using Unity.BossRoom.Utils;
using UnityEngine;
using VContainer;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
    /// <summary>
    /// Server-only component which publishes a message once the LifeState changes.
    /// </summary>
    [RequireComponent(typeof(NetworkLifeState), typeof(ServerCharacter))]
    public class PublishMessageOnLifeChange : NetworkBehaviour
    {
        NetworkLifeState m_NetworkLifeState;
        ServerCharacter m_ServerCharacter;

        [SerializeField]
        string m_CharacterName;

        NetworkNameState m_NameState;

        [Inject]
        IPublisher<LifeStateChangedEventMessage> m_Publisher;

        void Awake()
        {
            m_NetworkLifeState = GetComponent<NetworkLifeState>();
            m_ServerCharacter = GetComponent<ServerCharacter>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            m_NameState = GetComponent<NetworkNameState>();
            m_NetworkLifeState.LifeStateChanged += OnLifeStateChanged;

            var gameState = FindAnyObjectByType<ServerBossRoomState>();
            if (gameState != null)
            {
                gameState.Container.Inject(this);
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            m_NetworkLifeState.LifeStateChanged -= OnLifeStateChanged;
        }

        void OnLifeStateChanged(LifeState previousState, LifeState newState)
        {
            var lastDamager = m_ServerCharacter.LastDamager;
            m_Publisher.Publish(new LifeStateChangedEventMessage()
            {
                CharacterName = m_NameState != null ? m_NameState.Name : (FixedPlayerName)m_CharacterName,
                CharacterType = m_ServerCharacter.CharacterClass.CharacterType,
                NewLifeState = newState,
                ServerCharacter = m_ServerCharacter,
                KillerNetId = lastDamager != null ? lastDamager.netId : 0u,
                KilledByNpc = lastDamager != null && lastDamager.IsNpc
            });
        }
    }
}
