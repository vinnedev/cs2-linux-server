# CS2 Linux Server

## Project Overview

Dedicated Counter-Strike 2 server for Linux with modding support via CounterStrikeSharp (CSS) and Metamod:Source.

## Architecture

```
cs2-linux-server/
├── components/csgo/          # Pre-built addons, configs, and plugins ready for deployment
│   ├── addons/
│   │   ├── counterstrikesharp/
│   │   │   └── plugins/      # Compiled plugin DLLs (deployment target)
│   │   └── metamod/
│   └── cfg/                  # Server configuration files
├── plugins_source/            # C# source code for custom plugins
│   ├── cs2-autojoin/
│   ├── cs2-instadefuse/
│   ├── cs2-instaplant/
│   ├── cs2-inventory-simulator-plugin/
│   └── cs2-retakes/
├── server/                    # Runtime server directory (mounted as Docker volume)
├── scripts/
│   ├── build-plugins.sh       # Build and deploy plugins to components/
│   ├── check-updates.sh       # Check for mod/plugin updates
│   └── ...
├── docker-compose.yml
├── Dockerfile
├── .env.example               # Template for environment variables
├── install.sh                 # Full server setup (SteamCMD + CS2 + patches)
├── start.sh                   # Start server (copies components/ -> server/)
└── setup.sh                   # Alternative setup from upstream
```

## Deployment Flow

1. `components/csgo/` holds all addons and configs as the source of truth
2. `start.sh` copies `components/csgo/` into `server/game/csgo/` at each boot
3. `server/` is mounted as a Docker volume at `/app/server`
4. Plugin sources in `plugins_source/` build into `components/csgo/addons/counterstrikesharp/plugins/`

## Plugin Development (CounterStrikeSharp)

- Framework: CounterStrikeSharp (C# / .NET 8.0)
- API Version: 1.0.365
- Docs: https://docs.cssharp.dev/docs/guides/getting-started.html
- Auto-build: https://docs.cssharp.dev/docs/guides/auto-build-and-deploy.html
- Each plugin is a .csproj targeting `net8.0` with `CounterStrikeSharp.API` NuGet package
- Plugins are loaded from `addons/counterstrikesharp/plugins/<PluginName>/`
- Each plugin folder must contain the main DLL matching the plugin class name

### Build Commands

```bash
# Build all plugins and deploy to components/
./scripts/build-plugins.sh

# Build a specific plugin
./scripts/build-plugins.sh cs2-retakes

# Watch mode (auto-rebuild on changes)
./scripts/build-plugins.sh watch cs2-autojoin
```

### Adding a New Plugin

1. Create directory in `plugins_source/<plugin-name>/`
2. Create `.csproj` with:
   ```xml
   <PropertyGroup>
     <TargetFramework>net8.0</TargetFramework>
     <OutDir>./build/$(MSBuildProjectName)</OutDir>
     <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
   </PropertyGroup>
   <ItemGroup>
     <PackageReference Include="CounterStrikeSharp.API" Version="1.0.365" />
   </ItemGroup>
   ```
3. Add entry to `PLUGINS` array in `scripts/build-plugins.sh`

### Custom Plugins

| Plugin | Version | Description |
|--------|---------|-------------|
| cs2-autojoin | 1.1.0 | Auto team assignment on connect, force respawn, round-start spectator fix |
| cs2-retakes | 2.2.0 | Retakes mode with MongoDB VIP system, queue priority, bot management |
| cs2-instadefuse | - | Instant defuse plugin |
| cs2-instaplant | - | Instant plant plugin |
| cs2-inventory-simulator | - | Weapon skin simulator |

## Critical Rules

- NEVER modify files inside `server/` directly — they are overwritten on each `start.sh` run
- All persistent changes go in `components/csgo/`
- NEVER commit `.env` files, Steam credentials, or database connection strings
- NEVER hardcode secrets in source code — use environment variables
- NEVER delete `components/csgo/addons/` without backup — it contains all installed mods
- When updating mods, place files in `components/csgo/` not in `tmp/`
- `gameinfo.gi` patch (Metamod line) is applied by `install.sh` — do not remove it
- Use `AddTimer()` for delayed operations — NEVER use `Task.Delay` (runs outside game thread, causes crashes)
- MongoDB access must use `MongoDB.Instance` singleton — NEVER instantiate `new MongoDB()`

## Environment Variables

Required variables (see `.env.example`):

| Variable | Description |
|----------|-------------|
| PORT | Server port (default: 27015) |
| MONGODB_URI | MongoDB connection string for VIP/player system |
| API_KEY | Steam Web API key |
| STEAM_ACCOUNT | Steam Game Server Login Token |
| RCON_PASSWORD | Remote console password |

## Server Configuration

- Environment variables defined in `.env` (see `.env.example` for template)
- Game configs in `components/csgo/cfg/`
- Docker exposes ports 27015 (TCP/UDP) and 27020 (TCP/UDP)

## Update Workflow

```bash
# Check for updates and auto-deploy to components/
./scripts/check-updates.sh
```

Updates are automatically downloaded, extracted, and deployed to `components/csgo/` (addons + cfg).

## Tech Stack

- OS: Debian Bullseye (Docker)
- Runtime: .NET 8.0
- Game Server: CS2 Dedicated Server (SteamCMD appid 730)
- Framework: CounterStrikeSharp 1.0.365 + Metamod:Source
- Database: MongoDB (player data, VIP system)
- Language: C# for plugins, Bash for scripts
