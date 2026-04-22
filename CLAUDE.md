# CS2 Linux Server

## Project Overview

Vanilla dedicated Counter-Strike 2 server for Linux. No mods, no Metamod/CounterStrikeSharp. Plugin C# sources are preserved under `plugins_source/` for future use, but nothing is deployed by default.

## Architecture

```
cs2-linux-server/
├── plugins_source/         # C# plugin sources (preserved, not deployed)
├── server/                 # Runtime server directory (Docker volume)
│   ├── game/csgo/          # Vanilla CS2 install (from SteamCMD)
│   └── steamapps/
├── steamcmd/               # SteamCMD client (symlink)
├── scripts/
│   ├── _common.py          # Shared logger / utilities
│   ├── build_plugins.py    # Build plugin DLLs from plugins_source/
│   ├── get_map_names.py
│   └── parse_gamemodes.py
├── docker-compose.yml
├── Dockerfile
├── .env.example
├── install.py              # SteamCMD + CS2 install
├── start.py                # Launch the server
├── setup.py
├── runtime_net_install.py  # Optional .NET 8.0 runtime
└── healthcheck.py
```

## Boot Flow

1. `install.py` installs SteamCMD and runs `app_update 730 validate` into `server/`
2. `start.py` reads `.env`, opens firewall ports (host only), then launches `server/game/bin/linuxsteamrt64/cs2`
3. No overlay step — what's in `server/game/csgo/` is what runs

## Environment Variables

See `.env.example`. Required for a public server:

| Variable | Description |
|----------|-------------|
| PORT | Server port (default: 27015) |
| API_KEY | Steam Web API key |
| STEAM_ACCOUNT | Steam Game Server Login Token |
| RCON_PASSWORD | Remote console password |

## Critical Rules

- NEVER commit `.env`, Steam credentials, or tokens
- The server is vanilla — adding a plugin requires re-installing CounterStrikeSharp + Metamod manually
- Plugin sources in `plugins_source/` build with `dotnet build` against `CounterStrikeSharp.API` 1.0.365 (net8.0)

## Tech Stack

- OS: Debian Bullseye (Docker)
- Game Server: CS2 Dedicated Server (SteamCMD appid 730)
- Language: Python (scripts), C# (plugin sources only)
