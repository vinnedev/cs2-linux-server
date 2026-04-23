# Architecture

## System Design

```mermaid
flowchart TB
    subgraph Repo["Repository"]
        A["stack/vanilla"]
        B["plugins_source"]
        C["scripts/mod_stack.py"]
        D["scripts/build_plugins.py"]
        E["install.py"]
        F["setup.py"]
        G["start.py"]
        H[".env"]
    end

    subgraph Bootstrap["Bootstrap Layer"]
        I["SteamCMD"]
        J["CS2 install into server/"]
    end

    subgraph Runtime["Runtime server/"]
        K["server/game/csgo/gameinfo.gi"]
        L["server/game/csgo/addons/metamod"]
        M["server/game/csgo/addons/counterstrikesharp"]
        N["server/game/csgo/addons/counterstrikesharp/plugins"]
        O["server/game/csgo/cfg/<EXEC>"]
        P["server/game/bin/linuxsteamrt64/cs2"]
    end

    I --> E
    E --> J
    J --> K
    A --> C
    C --> K
    C --> L
    C --> M
    C --> O
    B --> D
    D --> N
    E --> C
    E --> D
    F --> C
    F --> D
    H --> G
    G --> P
    P --> L
    L --> M
    M --> N
```

## Responsibilities

### `stack/vanilla/`

Maintains the local baseline for:

- Metamod
- CounterStrikeSharp

### `plugins_source/`

Maintains custom plugin source code.

### `scripts/mod_stack.py`

Responsible for:

- patching `gameinfo.gi`
- applying the local stack
- fixing Linux permissions
- normalizing standard Metamod VDF/runtime files
- creating `cfg/<EXEC>`
- validating expected runtime files

### `scripts/build_plugins.py`

Responsible for:

- restore
- build
- deploy of custom plugins

### `install.py`

Responsible for the full bootstrap flow.

### `setup.py`

Responsible for reapplying the local baseline without reinstalling the game server.

### `start.py`

Responsible for starting the CS2 server process with the expected environment.

## Deployment Sequence

```mermaid
sequenceDiagram
    participant User
    participant Install as install.py/setup.py
    participant Env as .env
    participant Steam as SteamCMD
    participant Stack as stack/vanilla
    participant Build as build_plugins.py
    participant Runtime as server/game/csgo

    User->>Install: run command
    Install->>Env: load environment
    alt install.py
        Install->>Steam: install/update CS2
        Steam-->>Install: runtime server files
    end
    Install->>Runtime: patch gameinfo.gi
    Install->>Stack: read local baseline
    Stack-->>Install: addons payload
    Install->>Runtime: copy addons
    Install->>Runtime: normalize VDFs and permissions
    Install->>Runtime: ensure cfg/<EXEC>
    Install->>Build: build plugins
    Build->>Runtime: publish plugin binaries
```

## Healthy Runtime State

A healthy boot should show:

1. Metamod loaded
2. CounterStrikeSharp initialized
3. `AutojoinPlugin` loaded

After that point, remaining failures are usually related to:

- networking
- port binding
- Steam account token
- general server configuration
