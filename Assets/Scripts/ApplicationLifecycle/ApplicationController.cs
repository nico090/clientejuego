using System;
using Unity.BossRoom.ApplicationLifecycle.Messages;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.GameState;
using Unity.BossRoom.Gameplay.Messages;
using Unity.BossRoom.Infrastructure;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Unity.BossRoom.ApplicationLifecycle
{
    /// <summary>
    /// An entry point to the application, where we bind all the common dependencies to the root DI scope.
    /// </summary>
    public class ApplicationController : LifetimeScope
    {
        [SerializeField]
        UpdateRunner m_UpdateRunner;
        [SerializeField]
        ConnectionManager m_ConnectionManager;
        [SerializeField]
        BossRoomNetworkManager m_NetworkManager;

        IDisposable m_Subscriptions;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(m_UpdateRunner);
            builder.RegisterComponent(m_ConnectionManager);
            builder.RegisterComponent(m_NetworkManager);

            builder.Register<PersistentGameState>(Lifetime.Singleton);

            builder.RegisterInstance(new MessageChannel<QuitApplicationMessage>()).AsImplementedInterfaces();
            builder.RegisterInstance(new MessageChannel<ConnectStatus>()).AsImplementedInterfaces();
            builder.RegisterInstance(new MessageChannel<DoorStateChangedEventMessage>()).AsImplementedInterfaces();

            builder.RegisterInstance(new NetworkedMessageChannel<LifeStateChangedEventMessage>()).AsImplementedInterfaces();
            builder.RegisterInstance(new NetworkedMessageChannel<ConnectionEventMessage>()).AsImplementedInterfaces();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.RegisterInstance(new NetworkedMessageChannel<CheatUsedMessage>()).AsImplementedInterfaces();
#endif

            builder.RegisterInstance(new MessageChannel<ReconnectMessage>()).AsImplementedInterfaces();
        }

        private void Start()
        {
            var quitApplicationSub = Container.Resolve<ISubscriber<QuitApplicationMessage>>();

            var subHandles = new DisposableGroup();
            subHandles.Add(quitApplicationSub.Subscribe(QuitGame));
            m_Subscriptions = subHandles;

            Application.wantsToQuit += OnWantToQuit;
            DontDestroyOnLoad(gameObject);
            DontDestroyOnLoad(m_UpdateRunner.gameObject);
            Application.targetFrameRate = 120;

            // Dedicated servers skip MainMenu — DedicatedServerBootstrap handles the server lifecycle
            if (!IsDedicatedServer())
                SceneManager.LoadScene("MainMenu");
        }

        static bool IsDedicatedServer()
        {
#if UNITY_SERVER
            return true;
#else
            return System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--server") >= 0;
#endif
        }

        protected override void OnDestroy()
        {
            m_Subscriptions?.Dispose();
            base.OnDestroy();
        }

        private bool OnWantToQuit()
        {
            return true;
        }

        private void QuitGame(QuitApplicationMessage msg)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
