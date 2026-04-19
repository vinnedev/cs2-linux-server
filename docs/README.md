# CS2 Linux Server — Documentação Técnica

Servidor dedicado **Counter-Strike 2** para Linux com stack de mods (Metamod + CounterStrikeSharp), empacotado em Docker e configurável via `.env`.

## Índice

1. [Visão Geral](./01-overview.md) — propósito, stack e capacidades
2. [Arquitetura](./02-architecture.md) — diagramas de componentes e camadas
3. [Fluxo de Instalação](./03-install-flow.md) — do build ao primeiro boot
4. [Fluxo de Runtime](./04-runtime-flow.md) — o que acontece quando `start.sh` executa
5. [Sistema de Configuração](./05-config-system.md) — hierarquia de `.cfg`, gamemodes e hot-switch
6. [Addons e Plugins](./06-addons-plugins.md) — Metamod, CSS e plugins disponíveis
7. [Scripts Utilitários](./07-scripts.md) — `add-map`, `check-updates`, `parse-gamemodes`, etc.
8. [Deploy e Operação](./08-deploy-operation.md) — docker-compose, portas, RCON, troubleshooting

## Estrutura do Repositório

```
cs2-linux-server/
├── Dockerfile              # imagem Debian bullseye + libs 32/64 bits
├── docker-compose.yml      # orquestração do container cs2-server
├── .env.sample             # template de variáveis de ambiente
├── install.sh              # instalação local (host) via SteamCMD
├── setup.sh                # instalação remota modded (kus/cs2-modded-server)
├── start.sh                # entrypoint do container/host
├── runtime_net_install.sh  # instala .NET 8 runtime (dependência do CSS)
├── components/csgo/        # overlay que é copiado para game/csgo/ no boot
│   ├── addons/             # Metamod + CounterStrikeSharp + mods nativos
│   ├── cfg/                # configs de gamemodes, settings e plugins
│   ├── gamemodes_server.txt
│   └── subscribed_file_ids.txt
├── scripts/                # utilitários (maps, updates, patch gameinfo)
└── custom_files_example/   # exemplos para override pelo operador
```

## Início Rápido

```bash
cp .env.sample .env          # preencha STEAM_ACCOUNT, API_KEY, EXEC
./install.sh                 # baixa CS2 via SteamCMD + patch gameinfo.gi
./start.sh                   # sobe o servidor
# ou via docker
docker compose up -d --build
```
