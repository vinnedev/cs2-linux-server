# 02 — Arquitetura

## Diagrama de Componentes

```mermaid
flowchart TB
    subgraph Host["Host / Docker Engine"]
        direction TB
        subgraph Container["Container cs2-server (Debian bullseye)"]
            ENV[".env<br/>PORT, TICKRATE, EXEC,<br/>STEAM_ACCOUNT, API_KEY"]
            Start["start.py<br/>(entrypoint)"]
            Overlay["components/csgo/<br/>(overlay de mods e configs)"]

            subgraph Game["CS2 Dedicated Server"]
                CS2["cs2 binary<br/>linuxsteamrt64/cs2"]
                GameInfo["gameinfo.gi<br/>(+ patch Metamod)"]
                subgraph Addons["addons/"]
                    MM["Metamod Source"]
                    CSS["CounterStrikeSharp<br/>(.NET 8 runtime)"]
                    Misc["MovementUnlocker<br/>MultiAddonManager<br/>cs2fixes-rampbugfix"]
                end
                subgraph Cfg["cfg/"]
                    OnBoot["on_boot.cfg"]
                    ModeCfg["retake.cfg<br/>executes.cfg<br/>dm.cfg ..."]
                    Settings["settings/*.cfg"]
                end
                subgraph Plugins["CSS Plugins"]
                    Active["plugins/<br/>(habilitados)"]
                    Disabled["plugins/disabled/<br/>(carregados on-demand)"]
                end
            end
        end

        subgraph Volumes["Volumes"]
            V1["./server → /app/server"]
            V2["steamclient.so (host)"]
        end

        Ports["Portas expostas<br/>27015/tcp+udp<br/>27020/tcp+udp"]
    end

    Steam["Steam CDN"]
    Workshop["Steam Workshop<br/>(mapas subscritos)"]
    Players["Jogadores CS2"]

    ENV --> Start
    Start --> Overlay
    Overlay -.copia.-> Addons
    Overlay -.copia.-> Cfg
    Start --> CS2
    CS2 --> GameInfo
    GameInfo --> MM
    MM --> CSS
    CSS --> Active
    Active -.load/unload.-> Disabled
    CS2 --> ModeCfg
    ModeCfg --> OnBoot
    ModeCfg --> Settings

    Steam -->|install.py<br/>app_update 730| CS2
    Workshop -->|subscribed_file_ids.txt| CS2
    Players <-->|UDP 27015| CS2
    Players <-->|GOTV 27020| CS2
```

## Camadas Lógicas

```mermaid
graph LR
    A[Infraestrutura<br/>Docker + UFW] --> B[Runtime<br/>CS2 + libs 32/64]
    B --> C[Mod Layer<br/>Metamod + CSS]
    C --> D[Plugin Layer<br/>Retakes, MatchZy, etc.]
    D --> E[Config Layer<br/>.cfg por gamemode]
    E --> F[Operator Layer<br/>RCON + custom_files]
```

| Camada         | Responsabilidade                                    | Arquivos-chave                                |
|----------------|-----------------------------------------------------|-----------------------------------------------|
| Infraestrutura | Container, portas, firewall                         | `Dockerfile`, `docker-compose.yml`, `start.py`|
| Runtime        | Binário CS2 + SteamCMD + libs                       | `install.py`, `server/game/bin/.../cs2`       |
| Mod            | Hook Metamod em `gameinfo.gi`; bootstrap do CSS     | `addons/metamod`, `addons/counterstrikesharp` |
| Plugin         | Lógica de gamemode (Retakes, Executes, MatchZy...)  | `addons/counterstrikesharp/plugins/`          |
| Config         | Parâmetros do servidor por modo                     | `cfg/<mode>.cfg`, `cfg/settings/*.cfg`        |
| Operator       | Overrides e troca de modo em runtime                | `custom_files/`, RCON `!rcon exec <mode>`     |

## Princípio do Overlay

O diretório `components/csgo/` **não é** o servidor — é um pacote de artefatos que é copiado sobre `server/game/csgo/` a cada boot. Isso permite:

1. Versionar no git **apenas** o que é custom (addons, configs, map groups).
2. Re-sincronizar sempre que o SteamCMD atualiza o jogo e sobrescreve arquivos.
3. Manter `addons/` e `cfg/settings/` limpos antes da cópia (o `start.py` faz `rm -rf` desses diretórios antes de recopiar).

```mermaid
sequenceDiagram
    participant SCmd as SteamCMD
    participant Game as server/game/csgo
    participant Overlay as components/csgo
    participant Start as start.py

    SCmd->>Game: app_update 730 (arquivos originais)
    Start->>Game: rm -rf addons, cfg/settings
    Overlay->>Game: copytree_merge(components/csgo -> server/game/csgo)
    Start->>Game: exec CS2 binary
```
