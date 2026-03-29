# BossRoom PvP Arena

A multiplayer PvP arena game built with **Unity 6** and **Mirror Networking**, using a **FastAPI master server** for room matchmaking and dedicated game server orchestration.

## Tech Stack

- **Game Engine**: Unity 6000.0.52f1
- **Networking**: [Mirror](https://mirror-networking.com/) (KCP transport, UDP)
- **Master Server**: Python 3.12 + FastAPI + SQLite
- **Deployment**: Docker (master server) + Linux dedicated servers (spawned on-demand)

## Project Structure

```
/
├── Assets/
│   ├── Scripts/
│   │   ├── ConnectionManagement/    # Network state machine, Mirror integration
│   │   │   ├── Lobby/               # MasterServerClient, DedicatedServerBootstrap
│   │   │   └── ConnectionState/     # Offline, Connecting, Connected, Hosting, etc.
│   │   ├── Gameplay/
│   │   │   ├── GameState/           # Scene-specific server/client state logic
│   │   │   ├── GameplayObjects/     # Characters, spawners, actions
│   │   │   └── UI/Lobby/            # LobbyUIMediator, LobbyRoomEntry
│   │   └── Infrastructure/          # Scene loading, pub/sub messaging
│   ├── Scenes/                      # Startup, MainMenu, CharSelect, BossRoom, PostGame
│   └── Prefabs/                     # LobbyPanel, characters, UI elements
├── MasterServer/                    # FastAPI master server (Python)
│   ├── main.py                      # API endpoints
│   ├── room_manager.py              # Room lifecycle management
│   ├── process_manager.py           # Game server spawning (via host_agent)
│   ├── host_agent.py                # Runs on host, manages screen sessions
│   ├── config.py                    # Configuration from env vars
│   ├── database.py                  # SQLite persistence
│   ├── models.py                    # Pydantic request/response models
│   ├── docker-compose.yml           # Container deployment
│   └── .env                         # Environment configuration
├── Builds/                          # Build outputs
└── docs/                            # Architecture documentation
```

## Prerequisites

- **Unity** 6000.0.52f1 or later
- **Python** 3.12+ (for master server)
- **Docker** (optional, for containerized master server)

## Quick Start (Local Development)

### 1. Master Server

```bash
cd MasterServer
python -m venv venv
source venv/bin/activate          # Linux/Mac
# venv\Scripts\activate           # Windows
pip install -r requirements.txt
python main.py
```

The master server runs on `http://localhost:8000`.

### 2. Host Agent (required for spawning dedicated servers)

In a separate terminal:

```bash
cd MasterServer
python host_agent.py
```

Listens on `http://127.0.0.1:8099` by default.

### 3. Unity Client

1. Open the project in Unity 6000.0.52f1
2. Open `Assets/Scenes/Startup.unity`
3. Press Play
4. In the Main Menu, click **Lobby Browser** to connect to the master server

### 4. Testing Locally Without Master Server

Use **Direct IP** mode in the Main Menu:
- One instance clicks **Host** (starts a Mirror host on 127.0.0.1:7777)
- Other instances enter the IP and click **Connect**

## Game Flow

```
Startup -> MainMenu -> [Create/Join Room via Lobby] -> CharSelect -> BossRoom -> PostGame
```

## Configuration

See [MasterServer/README.md](MasterServer/README.md) for master server configuration.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for system architecture details.
