#!/bin/bash
set -e
set -u

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CS2_PATH="${CS2_PATH:-$ROOT_DIR/server/game/bin/linuxsteamrt64/cs2}"

ENV_FILE="$ROOT_DIR/.env"
if [ -f "$ENV_FILE" ]; then
    echo "🔄 Carregando variáveis de $ENV_FILE"
    export $(grep -v '^#' "$ENV_FILE" | xargs)
fi

# Variáveis de ambiente (defina no shell ou .env)
: "${PORT:=27015}"
: "${IP:=0.0.0.0}"
: "${TICKRATE:=64}"
: "${MAXPLAYERS:=16}"
: "${API_KEY:=}"
: "${STEAM_ACCOUNT:=}"
: "${LAN:=0}"
: "${SERVER_PASSWORD:=}"
: "${RCON_PASSWORD:=changeme}"
: "${EXEC:=autoexec.cfg}"

# Configura caminho de bibliotecas
export LD_LIBRARY_PATH="$ROOT_DIR/steamcmd/linux64:${LD_LIBRARY_PATH:-}"

# Abrindo portas no firewall
echo "🔐 Verificando/abrindo portas de firewall (UFW)..."
if command -v ufw &> /dev/null; then
    if ! sudo ufw status | grep -q "${PORT}/tcp"; then
        sudo ufw allow ${PORT}/tcp
    else
        echo "✅ Porta ${PORT}/tcp já está aberta no firewall."
    fi
    if ! sudo ufw status | grep -q "${PORT}/udp"; then
        sudo ufw allow ${PORT}/udp
    else
        echo "✅ Porta ${PORT}/udp já está aberta no firewall."
    fi
    if ! sudo ufw status | grep -q "27020/tcp"; then
        sudo ufw allow 27020/tcp
    else
        echo "✅ Porta 27020/tcp já está aberta no firewall."
    fi
    if ! sudo ufw status | grep -q "27020/udp"; then
        sudo ufw allow 27020/udp
    else
        echo "✅ Porta 27020/udp já está aberta no firewall."
    fi
else
    echo "⚠️ UFW não está instalado. Pulei a configuração de firewall."
fi

echo "===== Preparando arquivos do servidor ====="
rm -rf "$ROOT_DIR/server/game/csgo/addons"
rm -rf "$ROOT_DIR/server/game/csgo/cfg/settings"
cp -r "$ROOT_DIR/components/csgo/." "$ROOT_DIR/server/game/csgo/"

if [ -d "$ROOT_DIR/components/csgo/addons/linux" ]; then
    cp -r "$ROOT_DIR/components/csgo/addons/linux/." "$ROOT_DIR/server/game/csgo/"
fi

echo "🟢 Iniciando servidor CS2..."
"$CS2_PATH" -dedicated -console -usercon \
    +game_type 0 +game_mode 0 +mapgroup mg_active +map de_mirage \
    -port "$PORT" -ip "$IP" +net_public_adr "$IP" \
    -tickrate "$TICKRATE" +sv_visiblemaxplayers "$MAXPLAYERS" \
    -authkey "$API_KEY" +sv_setsteamaccount "$STEAM_ACCOUNT" \
    +sv_lan "$LAN" +sv_password "$SERVER_PASSWORD" +rcon_password "$RCON_PASSWORD" \
    +exec "$EXEC"

echo
echo "⚠️ CS2 foi encerrado ou travou. Pressione Enter para sair."
read
echo "🔴 Servidor CS2 encerrado."