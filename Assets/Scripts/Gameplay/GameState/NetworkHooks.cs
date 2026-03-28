using System;
using Mirror;

namespace Unity.BossRoom.Gameplay.GameState
{
    /// <summary>
    /// Mirror-based lifecycle hook component. Attach alongside a non-NetworkBehaviour MonoBehaviour
    /// that needs to react to Mirror's network spawn/despawn events.
    ///
    /// OnNetworkSpawn fires once per object instance:
    ///   - on the server (and host) via OnStartServer
    ///   - on a pure client via OnStartClient (skipped when isServer to avoid double-fire on host)
    /// Same de-duplication pattern for OnNetworkDespawn.
    /// </summary>
    public class NetworkHooks : NetworkBehaviour
    {
        public event Action OnNetworkSpawn;
        public event Action OnNetworkDespawn;

        public override void OnStartServer()
        {
            base.OnStartServer();
            OnNetworkSpawn?.Invoke();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!isServer)
                OnNetworkSpawn?.Invoke();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            OnNetworkDespawn?.Invoke();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!isServer)
                OnNetworkDespawn?.Invoke();
        }
    }
}
