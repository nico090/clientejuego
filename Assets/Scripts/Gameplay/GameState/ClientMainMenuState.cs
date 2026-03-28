using Unity.BossRoom.Gameplay.Configuration;
using Unity.BossRoom.Gameplay.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Unity.BossRoom.Gameplay.GameState
{
    /// <summary>
    /// Game Logic that runs when sitting at the MainMenu. This is likely to be "nothing", as no game has been started. But it is
    /// nonetheless important to have a game state, as the GameStateBehaviour system requires that all scenes have states.
    /// </summary>
    public class ClientMainMenuState : GameStateBehaviour
    {
        public override GameState ActiveState => GameState.MainMenu;

        [SerializeField]
        NameGenerationData m_NameGenerationData;
        [SerializeField]
        IPUIMediator m_IPUIMediator;
        [SerializeField]
        LobbyUIMediator m_LobbyUIMediator;

        protected override void Awake()
        {
            base.Awake();
            if (m_LobbyUIMediator != null) m_LobbyUIMediator.Hide();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(m_NameGenerationData);
            builder.RegisterComponent(m_IPUIMediator);
            if (m_LobbyUIMediator != null)
                builder.RegisterComponent(m_LobbyUIMediator);
        }

        public void OnDirectIPClicked()
        {
            if (m_LobbyUIMediator != null) m_LobbyUIMediator.Hide();
            m_IPUIMediator.Show();
        }

        public void OnLobbyBrowserClicked()
        {
            m_IPUIMediator.Hide();
            if (m_LobbyUIMediator != null) m_LobbyUIMediator.Show();
        }
    }
}
