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
    sudo ufw allow ${PORT}/tcp
    sudo ufw allow ${PORT}/udp
    sudo ufw allow 27020/tcp
    sudo ufw allow 27020/udp
else
    echo "⚠️ UFW não está instalado. Pulei a configuração de firewall."
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