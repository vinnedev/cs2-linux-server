# 03 — Fluxo de Instalação

Há tres caminhos suportados: **instalação direta no host Linux** (`install.sh`), **instalação local no Windows** (`install.ps1`) e **container Docker** (`docker compose up --build`). Todos convergem no mesmo layout de arquivos.

## Instalação Local (`install.sh`)

```mermaid
flowchart TD
    Start([./install.sh]) --> Deps["[1/5] apt install<br/>lib32gcc-s1, lib32stdc++6,<br/>curl, wget, screen, tar"]
    Deps --> SCmd{"steamcmd.sh<br/>existe?"}
    SCmd -- não --> DL["wget steamcmd_linux.tar.gz<br/>tar -xvzf"]
    SCmd -- sim --> Update
    DL --> Update["[3/5] steamcmd<br/>+login anonymous<br/>+app_update 730 validate"]
    Update --> Patch["[4/5] patch gameinfo.gi<br/>(insere Game csgo/addons/metamod)"]
    Patch --> Backup{"gameinfo.gi.bak<br/>existe?"}
    Backup -- não --> Bak[cria backup]
    Backup -- sim --> Overlay
    Bak --> Overlay
    Patch --> Overlay["[5/5] aplicar overlay<br/>rm -rf addons + cfg/settings<br/>cp -r components/csgo/."]
    Overlay --> Linux{"addons/linux/<br/>existe?"}
    Linux -- sim --> CopyLinux[copia linux-specific binaries]
    Linux -- não --> Done
    CopyLinux --> Done([✅ Pronto para ./start.sh])
```

### Etapas-chave

| Etapa | O que faz                                              | Por quê                                                |
|-------|--------------------------------------------------------|--------------------------------------------------------|
| 1     | Instala libs de 32-bit                                 | SteamCMD e algumas libs do CS2 precisam delas          |
| 2     | Baixa SteamCMD em `./steamcmd/`                        | Gerenciador oficial de downloads de apps Steam         |
| 3     | `app_update 730 validate` em `./server/`               | Baixa o servidor dedicado (anônimo)                    |
| 4     | Patch em `gameinfo.gi` inserindo entrada do Metamod    | Sem isso, o jogo não carrega o loader de plugins       |
| 5     | Overlay: limpa `addons/` + `cfg/settings/` e recopia   | Garante estado determinístico dos mods                 |

## Instalação via Docker

```mermaid
sequenceDiagram
    participant User
    participant Compose as docker-compose
    participant Build as Dockerfile
    participant CT as Container cs2-server
    participant Host as Host filesystem

    User->>Compose: docker compose up --build
    Compose->>Build: FROM debian:bullseye<br/>apt install libs<br/>COPY components, scripts, .env
    Build->>CT: imagem cs2-server
    Compose->>CT: cria container<br/>expõe 27015, 27020<br/>monta ./server e steamclient.so
    CT->>CT: CMD ["/app/start.sh"]
    CT->>Host: escreve em /app/server (volume)
```

### docker-compose.yml — pontos relevantes

- **`env_file: .env`** — injeta as mesmas variáveis que `start.sh` consome.
- **`ports`** — 27015 (jogo/RCON) e 27020 (GOTV) em TCP e UDP.
- **`volumes`**:
  - `./server:/app/server` — persiste o download do SteamCMD fora do container.
  - `steamclient.so` — bind mount do host para satisfazer a dependência dinâmica do binário.

## Instalação Local no Windows (`install.ps1`)

Fluxo resumido:

1. Carrega `.env` se existir e respeita `CS2_PATH` quando informado.
2. Instala `steamcmd.exe` em `.\steamcmd\` se necessário.
3. Procura uma instalacao existente em `CS2_PATH`, em `.\server\` ou nas bibliotecas Steam do Windows.
4. Se encontrar, cria `server/` como junction para a pasta existente; se nao encontrar, executa `+app_update 730`.
5. Aplica o patch de `gameinfo.gi` e copia `components/csgo/` com overlay de `addons/windows/`.

Esse fluxo evita baixar tudo de novo quando o host ja possui o CS2 instalado em outra pasta.
Quando uma pasta existente e reaproveitada, o patch do Metamod e os arquivos do overlay passam a ser aplicados nela atraves da junction `server/`.

## Patch do `gameinfo.gi`

```mermaid
flowchart LR
    A[gameinfo.gi original] --> B{contém<br/>Game csgo/addons/metamod?}
    B -- sim --> D[skip]
    B -- não --> C[awk insere linha<br/>após Game_LowViolence csgo_lv]
    C --> E[gameinfo.gi patched]
    A -.backup.-> F[gameinfo.gi.bak]
```

Sem essa linha, Metamod não é carregado e, por consequência, CounterStrikeSharp (que roda como plugin do Metamod) também não.

## Setup remoto alternativo (`setup.sh`)

`setup.sh` é um instalador **pull-based** que baixa artefatos do repositório [`kus/cs2-modded-server`](https://github.com/kus/cs2-modded-server) — usado para aprovisionar rapidamente uma VM nova. Ele:

1. Baixa `stop.sh` e `start.sh` via curl direto do GitHub.
2. Resolve IP público com OpenDNS.
3. Instala SteamCMD em `/steamcmd` com symlinks para `~/.steam/sdk32/` e `~/.steam/sdk64/`.
4. `app_update 730` em `~/cs2_server`.
5. Baixa o zip do mod, mescla `custom_files/` sobre `game/csgo/`.
6. Aplica patch do Metamod e inicia o servidor.

> Use `install.sh` para instalações baseadas neste repositório; `setup.sh` é utilitário para reprovisão rápida com o fork original.
