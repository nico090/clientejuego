# Architecture

## System Overview

```
 +-----------------+         +-----------------------+         +------------------+
 |  Unity Client   | --HTTP->|   Master Server       | --HTTP->|   Host Agent     |
 |  (LobbyUI +    |         |   (FastAPI, Docker)    |         |   (host machine) |
 |   Mirror client)|         +-----------------------+         +------------------+
 |                 |                                                   |
 |                 |         +------------------------+                |
 |                 | <-KCP-->| Dedicated Game Server  |<--- spawns ----+
 |                 |  (UDP)  | (Unity headless,       |   (screen session)
 +-----------------+         |  Mirror server)        |
                             +------------------------+
                                      |
                                      | heartbeat (HTTP POST every 3s)
                                      v
                             +------------------------+
                             |    Master Server       |
                             +------------------------+
```

**Components:**

- **Unity Client**: The game running on a player's machine. Displays lobby UI, connects to game servers via Mirror (KCP/UDP).
- **Master Server**: FastAPI service managing rooms, matchmaking, and game server lifecycle. Runs in Docker with `network_mode: host`.
- **Host Agent**: Lightweight HTTP server running on the host machine. Receives spawn/kill commands from the master server and manages game server processes as `screen` sessions.
- **Dedicated Game Server**: Headless Unity build running Mirror in server-only mode. Sends heartbeats to the master server and handles gameplay.

## Room Creation Flow

```
Client                    Master Server              Host Agent           Game Server
  |                            |                        |                     |
  |-- POST /api/rooms -------->|                        |                     |
  |                            |-- allocate port ------>|                     |
  |                            |-- POST /spawn -------->|                     |
  |                            |                        |-- screen -dmS ----->|
  |                            |<-- {ok: true} ---------|                     |
  |                            |-- db_upsert_room       |                     |
  |                            |-- generate room_key    |                     |
  |<-- {room_id, port, key} ---|                        |                     |
  |                            |                        |                     |
  |  [poll /api/rooms/{id}/status every 1.5s]           |                     |
  |                            |                        |                     |
  |                            |                        |     [Unity starts]  |
  |                            |                        |     [Mirror binds]  |
  |                            |<-- POST /api/heartbeat (status="ready") -----|
  |                            |-- update room status   |                     |
  |                            |                        |                     |
  |-- GET /api/rooms/{id}/status ->|                    |                     |
  |<-- {status: "ready"} ------|                        |                     |
  |                            |                        |                     |
  |===================== KCP/UDP CONNECTION =============================>|
  |-- ConnectionPayloadMessage (playerId, playerName, roomKey) ---------->|
  |                            |                        |                     |
  |                            |<-- POST /api/validate-key (roomKey) ---------|
  |                            |-- {valid: true, player_name} ------------->|
  |                            |                        |                     |
  |<======================== GAME SESSION ================================>|
```

## Room Join Flow

```
Client                    Master Server                        Game Server
  |                            |                                    |
  |-- GET /api/rooms --------->|                                    |
  |<-- [room list] ------------|                                    |
  |                            |                                    |
  |  [user clicks Join]        |                                    |
  |                            |                                    |
  |-- POST /api/rooms/join --->|                                    |
  |                            |-- validate password, capacity      |
  |                            |-- generate room_key                |
  |<-- {host, port, key} ------|                                    |
  |                            |                                    |
  |===================== KCP/UDP CONNECTION ======================>|
  |-- ConnectionPayloadMessage (playerId, playerName, roomKey) --->|
  |                            |                                    |
  |                            |<-- POST /api/validate-key ---------|
  |                            |-- {valid, player_name} ----------->|
  |                            |                                    |
  |<======================== GAME SESSION ========================>|
```

## Connection State Machine (Client)

The `ConnectionManager` uses a state machine to manage the client's connection lifecycle:

```
                    StartClientIP()
  [OfflineState] ─────────────────────> [ClientConnectingState]
       |                                       |
       |  StartHostIP()                        | OnClientConnected
       v                                       v
  [StartingHostState]                  [ClientConnectedState]
       |                                       |
       | OnServerStarted                       | OnClientDisconnect
       v                                       v
  [HostingState]                       [ClientReconnectingState]
       |                                    |        |
       | OnShutdown                  success |        | max retries
       v                                    v        v
  [OfflineState]                [ClientConnectingState]  [OfflineState]
```

**States:**
- **OfflineState**: Network shut down. Entry point. Loads MainMenu scene.
- **ClientConnectingState**: Client attempting to connect. Sets up transport address/port, calls `StartClient()`.
- **ClientConnectedState**: Connected to server. Gameplay active.
- **ClientReconnectingState**: Automatic reconnection with exponential backoff (max 2 attempts).
- **StartingHostState**: Initializing as host. Sets up payload, calls `StartHost()`.
- **HostingState**: Server running, accepting clients. Loads CharSelect scene.

## Scene Flow

```
Startup (bootstrap)
    |
    v
MainMenu (lobby browser, direct IP)
    |
    v
CharSelect (character/seat selection, synced via Mirror)
    |
    v
BossRoom (PvP arena gameplay)
    |
    v
PostGame (results screen)
    |
    v
MainMenu (loop back)
```

## Authentication: Room Keys

Room keys are one-time-use cryptographic tokens that authorize a player to connect to a specific game server:

1. **Generation**: Master server generates a `secrets.token_urlsafe(32)` when a room is created (for the creator) or when a player joins via `/api/rooms/join`.
2. **Delivery**: The key is returned to the client in the HTTP response.
3. **Transmission**: The client includes the key in `ConnectionPayload.roomKey`, sent to the game server as a `ConnectionPayloadMessage` immediately after TCP connection.
4. **Validation**: The game server calls `POST /api/validate-key` on the master server to verify and consume the key.
5. **Expiry**: Keys expire after 90 seconds if not consumed. Each key can only be used once.

This prevents unauthorized players from connecting directly to game server ports without going through the master server's matchmaking.

## Heartbeat System

Dedicated game servers send heartbeats to the master server every 3 seconds:

```json
{
  "room_id": "...",
  "server_secret": "shared-secret",
  "current_players": 2,
  "status": "ready"
}
```

The master server uses heartbeats to:
- Track room status transitions (`starting` -> `ready` -> `in_game` -> `closing`)
- Monitor player counts
- Detect dead servers (no heartbeat for 45s -> kill room)
- Clean up empty rooms (0 players for 120s -> kill room)

## Transport & Ports

- **Mirror Transport**: KCP (reliable UDP), configurable via Unity Inspector
- **Port allocation**: Master server assigns ports from range 7770-7870
- **Port setting**: `BossRoomNetworkManager.SetTransportPort()` uses reflection to set the port on the active transport (supports KcpTransport `Port` field and TelepathyTransport `port` field)
- **Master server**: Runs on port 8000 (HTTP)
- **Host agent**: Runs on port 8099 (HTTP, localhost only)

## Key Files Reference

| Component | File | Purpose |
|-----------|------|---------|
| Network Manager | `Assets/Scripts/ConnectionManagement/BossRoomNetworkManager.cs` | Mirror NetworkManager subclass |
| Connection States | `Assets/Scripts/ConnectionManagement/ConnectionState/*.cs` | State machine for connections |
| Connection Manager | `Assets/Scripts/ConnectionManagement/ConnectionManager.cs` | State machine orchestrator |
| Lobby UI | `Assets/Scripts/Gameplay/UI/Lobby/LobbyUIMediator.cs` | Room browser, create, join UI |
| Room Entry | `Assets/Scripts/Gameplay/UI/Lobby/LobbyRoomEntry.cs` | Single room display in list |
| Master Server Client | `Assets/Scripts/ConnectionManagement/Lobby/MasterServerClient.cs` | HTTP client for master server |
| Server Bootstrap | `Assets/Scripts/ConnectionManagement/Lobby/DedicatedServerBootstrap.cs` | Headless server startup + heartbeat |
| Room Data | `Assets/Scripts/ConnectionManagement/Lobby/RoomData.cs` | C# DTOs matching API models |
| Master Server | `MasterServer/main.py` | FastAPI application |
| Room Manager | `MasterServer/room_manager.py` | Room state, port allocation |
| Process Manager | `MasterServer/process_manager.py` | Game server spawning via host agent |
| Host Agent | `MasterServer/host_agent.py` | Host-side process manager |
| Config | `MasterServer/config.py` | Environment configuration |
