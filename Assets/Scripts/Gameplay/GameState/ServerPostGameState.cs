using Mirror;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Infrastructure;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace Unity.BossRoom.Gameplay.GameState
{
    [RequireComponent(typeof(NetworkHooks))]
    public class ServerPostGameState : GameStateBehaviour
    {
        [FormerlySerializedAs("m_NetcodeHooks")]
        [SerializeField]
        NetworkHooks m_NetworkHooks;

        [FormerlySerializedAs("synchronizedStateData")]
        [SerializeField]
        NetworkPostGame networkPostGame;
        public NetworkPostGame NetworkPostGame => networkPostGame;

        public override GameState ActiveState { get { return GameState.PostGame; } }

        [Inject]
        ConnectionManager m_ConnectionManager;

        [Inject]
        PersistentGameState m_PersistentGameState;

        protected override void Awake()
        {
            base.Awake();
            m_NetworkHooks.OnNetworkSpawn += OnNetworkSpawn;
        }

        void OnNetworkSpawn()
        {
            if (!NetworkServer.active)
            {
                enabled = false;
            }
            else
            {
                SessionManager<SessionPlayerData>.Instance.OnSessionEnded();
                networkPostGame.WinState = m_PersistentGameState.WinState;

                // Pass PvP final scores to clients
                if (PvPScoreManager.Instance != null)
                {
                    string scoresJson = PvPScoreManager.Instance.GetFinalScoresJson();
                    if (!string.IsNullOrEmpty(scoresJson))
                    {
                        networkPostGame.FinalScoresJson = scoresJson;
                    }
                }
            }
        }

        protected override void OnDestroy()
        {
            //clear actions pool
            ActionFactory.PurgePooledActions();
            m_PersistentGameState.Reset();

            base.OnDestroy();

            m_NetworkHooks.OnNetworkSpawn -= OnNetworkSpawn;
        }

        public void PlayAgain()
        {
            SceneLoaderWrapper.Instance.LoadScene("CharSelect", useNetworkSceneManager: true);
        }

        public void GoToMainMenu()
        {
            m_ConnectionManager.RequestShutdown();
        }
    }
}
