# 04 — Fluxo de Runtime

## `start.py` — Sequência de Boot

```mermaid
sequenceDiagram
    autonumber
    participant Script as start.py
    participant Env as .env
    participant UFW as UFW firewall
    participant FS as Filesystem
    participant CS2 as cs2 binary
    participant MM as Metamod
    participant CSS as CounterStrikeSharp
    participant Cfg as retake.cfg / EXEC

    Script->>Env: load_env_file + export_env
    Script->>Script: defaults (PORT=27015, TICKRATE=128, EXEC=retake.cfg)
    Script->>UFW: ufw allow 27015/tcp+udp, 27020/tcp+udp
    Script->>FS: rm -rf server/game/csgo/addons
    Script->>FS: rm -rf server/game/csgo/cfg/settings
    Script->>FS: copytree_merge(components/csgo -> server/game/csgo)
    Script->>CS2: subprocess.Popen cs2 -dedicated -console -usercon +args
    CS2->>MM: lê gameinfo.gi, carrega Metamod
    MM->>CSS: carrega CounterStrikeSharp via vdf
    CS2->>Cfg: +exec $EXEC (ex: retake.cfg)
    Cfg->>CSS: css_plugins load RetakesPlugin, InstadefusePlugin...
    Cfg->>Cfg: exec retake_settings.cfg, bots.cfg, env.cfg
    CS2-->>Shell: servidor em execução (bloqueia)
```

## Argumentos passados ao binário

```bash
cs2 -dedicated -console -usercon \
    +game_type 0 +game_mode 0 +mapgroup mg_active +map de_mirage \
    -port $PORT -ip $IP +net_public_adr $IP \
    -tickrate $TICKRATE +sv_visiblemaxplayers $MAXPLAYERS \
    -authkey $API_KEY +sv_setsteamaccount $STEAM_ACCOUNT \
    +sv_lan $LAN +sv_password $SERVER_PASSWORD +rcon_password $RCON_PASSWORD \
    +exec $EXEC
```

| Flag                        | Origem / função                                        |
|-----------------------------|--------------------------------------------------------|
| `-dedicated -console`       | Modo dedicated sem GUI                                 |
| `-usercon`                  | Permite RCON externo                                   |
| `-authkey`                  | Game Server Login Token (Steam Web API Key)            |
| `+sv_setsteamaccount`       | GSLT da conta Steam (obrigatório em partidas públicas) |
| `+exec $EXEC`               | Gamemode inicial (define hostname, plugins, regras)    |
| `+map de_mirage`            | Mapa placeholder até o `.cfg` trocar via mapgroup      |

## Cadeia de execução de configs

```mermaid
flowchart TD
    EXEC[".env: EXEC=retake.cfg"] --> RetakeCfg["retake.cfg<br/>- hostname<br/>- sv_tags<br/>- game_type/mode<br/>- css_plugins load"]
    RetakeCfg --> Unload["unload_plugins.cfg<br/>(descarrega outros modos)"]
    RetakeCfg --> RetakeSettings["retake_settings.cfg"]
    RetakeSettings --> Comp["gamemode_competitive.cfg"]
    RetakeSettings --> Retakes["cs2-retakes/retakes.cfg"]
    RetakeSettings --> MapVote["settings/map_voting.cfg"]
    RetakeSettings --> Bots["settings/bots_dont_buy.cfg"]
    RetakeCfg --> AfterMap["exec_after_map_start<br/>retake_settings.cfg<br/>(re-aplica a cada mapa)"]

    style RetakeCfg fill:#264653,color:#fff
    style Unload fill:#e76f51,color:#fff
    style AfterMap fill:#2a9d8f,color:#fff
```

## Troca de gamemode em runtime

```mermaid
stateDiagram-v2
    [*] --> Retake: EXEC=retake.cfg no boot
    Retake --> Executes: !rcon exec executes
    Executes --> Deathmatch: !rcon exec dm
    Deathmatch --> GunGame: !rcon exec gg
    GunGame --> Practice: !rcon exec prac
    Practice --> Retake: !rcon exec retake
    state Retake {
        [*] --> loadRetakesPlugin
        loadRetakesPlugin --> loadInstadefuse
        loadInstadefuse --> applySettings
    }
```

Cada `<modo>.cfg` começa com `exec unload_plugins.cfg`, que descarrega plugins de modos anteriores para evitar colisões (ex: `RetakesPlugin` interferindo com `MatchZy`). Em seguida, carrega explicitamente os DLLs do modo alvo a partir de `plugins/disabled/`.

## Ciclo por mapa

O `exec_after_map_start` (provido pelo plugin `CS2_ExecAfter`) reexecuta o `_settings.cfg` do modo a cada troca de mapa, garantindo que cvars sensíveis à persistência (`mp_roundtime`, `sv_cheats`, etc.) sejam reaplicados.

## Observabilidade mínima

- `log on` / `sv_logecho 1` — habilitam logs textuais (desabilitados por padrão em `server.cfg`).
- `writeid` / `writeip` — persistem listas de banimento após boot.
- **RCON** (porta 27015 TCP) — único canal administrativo remoto.
