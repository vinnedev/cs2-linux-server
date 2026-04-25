# CS2 Linux Server

## Project Overview

Counter-Strike 2 dedicated server for Linux with a local, versioned vanilla mod stack:

- `Metamod:Source`
- `CounterStrikeSharp`

Custom plugins live in `plugins_source/` and are built/deployed automatically into the runtime server after the vanilla stack is applied.

## Architecture

```text
cs2-linux-server/
├── plugins_source/         # C# plugin sources
├── scripts/
│   ├── _common.py          # Shared logger / utilities
│   ├── build_plugins.py    # Build + deploy plugins from plugins_source/
│   └── mod_stack.py        # Patch gameinfo + apply local vanilla stack
├── stack/
│   └── vanilla/
│       ├── addons/         # Local baseline for Metamod + CounterStrikeSharp
│       ├── README.md
│       └── VERSIONS.json
├── server/                 # Runtime CS2 server install
│   ├── game/csgo/
│   └── steamapps/
├── steamcmd/               # SteamCMD client
├── install.py              # First-time install: CS2 + local stack + plugins
├── setup.py                # Reapply local stack + plugins to existing server
├── start.py                # Launch the server
└── healthcheck.py
```

## Install Flow

`python3 install.py` is the first-time bootstrap:

1. installs system dependencies when available
2. installs SteamCMD if needed
3. installs or updates CS2 into `server/`
4. patches `server/game/csgo/gameinfo.gi` for Metamod
5. copies `stack/vanilla/addons/` into `server/game/csgo/addons/`
6. fixes executable bits for CounterStrikeSharp and Metamod `.so` files
7. creates the configured `cfg/<EXEC>` file if it does not exist
8. builds and deploys plugins from `plugins_source/`

## Setup Flow

`python3 setup.py` is idempotent and intended for an already-installed server:

1. patches `gameinfo.gi` if needed
2. reapplies the local vanilla stack from `stack/vanilla/addons/`
3. creates the configured `cfg/<EXEC>` if missing
4. rebuilds and redeploys plugins

Use this after changing:

- the local stack in `stack/vanilla/`
- plugin code in `plugins_source/`
- server files that need the baseline reapplied

## Boot Flow

`python3 start.py`:

1. loads `.env`
2. opens firewall ports when possible
3. sets `LD_LIBRARY_PATH`
4. launches `server/game/bin/linuxsteamrt64/cs2`

Expected runtime state after a successful setup:

- Metamod loads from `server/game/csgo/addons/metamod`
- CounterStrikeSharp loads from `server/game/csgo/addons/counterstrikesharp`
- plugins load from `server/game/csgo/addons/counterstrikesharp/plugins`

## Environment Variables

See `.env.example`. Important keys:

| Variable | Description |
|----------|-------------|
| `PORT` | Server port |
| `IP` | Bind IP |
| `TICKRATE` | Tickrate |
| `MAXPLAYERS` | Visible max players |
| `API_KEY` | Steam Web API key |
| `STEAM_ACCOUNT` | Steam Game Server Login Token |
| `RCON_PASSWORD` | RCON password |
| `EXEC` | Config file executed on start, e.g. `retake.cfg` |
| `BUILD_PLUGINS` | Set to `0` to skip plugin build/deploy |

## Critical Rules

- NEVER commit `.env`, Steam credentials, or tokens.
- Treat `stack/vanilla/` as the local source of truth for the vanilla mod stack.
- Do not put custom plugins into `stack/vanilla/`.
- Custom plugins must come from `plugins_source/` and be deployed by `scripts/build_plugins.py`.
- If `start.py` shows load failures for Metamod or CounterStrikeSharp, first verify `setup.py` has been run against the current local stack.

## Current Baseline

See [stack/vanilla/VERSIONS.json](/home/vinicius/projects/cs2-linux-server/stack/vanilla/VERSIONS.json).
