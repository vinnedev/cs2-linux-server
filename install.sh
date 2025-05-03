#!/bin/bash
set -e
set -u

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STEAMCMD_DIR="$ROOT_DIR/steamcmd"
STEAMCMD_SH="$STEAMCMD_DIR/steamcmd.sh"
GAMEINFO_PATH="$ROOT_DIR/server/game/csgo/gameinfo.gi"
SEARCH_STRING='Game	csgo/addons/metamod'
INSERT_AFTER='Game_LowViolence	csgo_lv'
BAK_FILE="$GAMEINFO_PATH.bak"
TEMP_FILE="$GAMEINFO_PATH.tmp"

echo "===== [1/5] Verificando dependências ====="
sudo apt update
sudo apt install lib32gcc-s1 lib32stdc++6 curl wget screen tar -y

echo "===== [2/5] Instalando SteamCMD (se necessário) ====="
if [ ! -f "$STEAMCMD_SH" ]; then
    mkdir -p "$STEAMCMD_DIR"
    cd "$STEAMCMD_DIR"
    wget https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz
    tar -xvzf steamcmd_linux.tar.gz
    cd "$ROOT_DIR"
else
    echo "SteamCMD já está instalado."
fi

echo "===== [3/5] Instalando CS2 via SteamCMD (appid 730) ====="
mkdir -p "$ROOT_DIR/server"
"$STEAMCMD_SH" +force_install_dir "$ROOT_DIR/server" +login anonymous +app_update 730 validate +quit

echo "===== [4/5] Patch de gameinfo.gi ====="
if [ ! -f "$GAMEINFO_PATH" ]; then
    echo "Erro: gameinfo.gi não encontrado em $GAMEINFO_PATH"
    exit 1
fi

if [ ! -f "$BAK_FILE" ]; then
    cp "$GAMEINFO_PATH" "$BAK_FILE"
    echo "Backup criado: $BAK_FILE"
fi

if grep -Fq "$SEARCH_STRING" "$GAMEINFO_PATH"; then
    echo "Patch já aplicado."
else
    echo "Aplicando patch no gameinfo.gi..."
    awk -v insertAfter="$INSERT_AFTER" -v toInsert="$SEARCH_STRING" '
        { print }
        $0 == insertAfter { print "\t\t\t" toInsert }
    ' "$GAMEINFO_PATH" > "$TEMP_FILE" && mv "$TEMP_FILE" "$GAMEINFO_PATH"
fi

echo "===== [5/5] Preparando arquivos do servidor ====="
rm -rf "$ROOT_DIR/server/game/csgo/addons"
rm -rf "$ROOT_DIR/server/game/csgo/cfg/settings"
cp -r "$ROOT_DIR/components/csgo/." "$ROOT_DIR/server/game/csgo/"

if [ -d "$ROOT_DIR/components/csgo/addons/linux" ]; then
    cp -r "$ROOT_DIR/components/csgo/addons/linux/." "$ROOT_DIR/server/game/csgo/"
fi


echo "✅ Setup concluído. Para iniciar o servidor, execute ./start.sh"

