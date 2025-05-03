#!/usr/bin/env bash

# Variables
BRANCH="master"

# Check if MOD_BRANCH is set and not empty
if [ -n "$MOD_BRANCH" ]; then
  BRANCH="$MOD_BRANCH"
fi

CUSTOM_FILES="${CUSTOM_FOLDER:-custom_files}"

# 32 or 64 bit Operating System
if [ -z "$BITS" ]; then
  architecture=$(uname -m)
  if [[ $architecture == *"64"* ]]; then
    export BITS=64
  elif [[ $architecture == *"i386"* ]] || [[ $architecture == *"i686"* ]]; then
    export BITS=32
  else
    echo "Unknown architecture: $architecture"
    exit 1
  fi
fi

if [[ -z $IP ]]; then
  IP_ARGS=""
else
  IP_ARGS="-ip ${IP}"
fi

if [ -f /etc/os-release ]; then
  . /etc/os-release
  DISTRO_OS=$NAME
  DISTRO_VERSION=$VERSION_ID
else
  DISTRO_OS=$(uname -s)
  DISTRO_VERSION=$(uname -r)
fi

echo "Starting on $DISTRO_OS: $DISTRO_VERSION..."

FREE_SPACE=$(df / --output=avail -BG | tail -n 1 | tr -d 'G')
echo "With $FREE_SPACE Gb free space..."

if ! command -v apt-get &> /dev/null; then
  echo "ERROR: OS distribution not supported (apt-get not available). $DISTRO_OS: $DISTRO_VERSION"
  exit 1
fi

if [ "$EUID" -ne 0 ]; then
  echo "ERROR: Please run this script as root..."
  exit 1
fi

curl -s -H "Cache-Control: no-cache" -o "stop.sh" "https://raw.githubusercontent.com/kus/cs2-modded-server/${BRANCH}/stop.sh" && chmod +x stop.sh
curl -s -H "Cache-Control: no-cache" -o "start.sh" "https://raw.githubusercontent.com/kus/cs2-modded-server/${BRANCH}/start.sh" && chmod +x start.sh

PUBLIC_IP=$(dig -4 +short myip.opendns.com @resolver1.opendns.com)
if [ -z "$PUBLIC_IP" ]; then
  echo "ERROR: Cannot retrieve your public IP address..."
  exit 1
fi

if [ ! -z "$DUCK_TOKEN" ]; then
  echo url="http://www.duckdns.org/update?domains=$DUCK_DOMAIN&token=$DUCK_TOKEN&ip=$PUBLIC_IP" | curl -k -o /duck.log -K -
fi

echo "Checking steamcmd exists..."
if [ ! -d "/steamcmd" ]; then
  mkdir /steamcmd && cd /steamcmd
  wget https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz
  tar -xvzf steamcmd_linux.tar.gz
  mkdir -p /root/.steam/sdk32/
  ln -s /steamcmd/linux32/steamclient.so /root/.steam/sdk32/
  mkdir -p /root/.steam/sdk64/
  ln -s /steamcmd/linux64/steamclient.so /root/.steam/sdk64/
fi

echo "Downloading any updates for CS2..."
/steamcmd/steamcmd.sh \
  +api_logging 1 1 \
  +@sSteamCmdForcePlatformType linux \
  +@sSteamCmdForcePlatformBitness $BITS \
  +force_install_dir ~/cs2_server \
  +login anonymous \
  +app_update 730 \
  +quit

mkdir -p ~/.steam/sdk32/
ln -s /steamcmd/linux32/steamclient.so ~/.steam/sdk32/
mkdir -p ~/.steam/sdk64/
ln -s /steamcmd/linux64/steamclient.so ~/.steam/sdk64/

if [ "${DISTRO_OS}" == "Ubuntu" ] && [ "${DISTRO_VERSION}" == "22.04" ]; then
  rm ~/cs2_server/bin/libgcc_s.so.1
fi

rm -r ~/cs2_server/game/csgo/addons
rm -r ~/cs2_server/game/csgo/cfg/settings

echo "Downloading mod files..."
wget --quiet https://github.com/kus/cs2-modded-server/archive/${BRANCH}.zip
unzip -o -qq ${BRANCH}.zip
rm -r ~/cs2_server/custom_files_example/
cp -R cs2-modded-server-${BRANCH}/custom_files_example/ ~/cs2_server/custom_files_example/
cp -R cs2-modded-server-${BRANCH}/game/csgo/ ~/cs2_server/game/
if [ ! -d "~/cs2_server/custom_files/" ]; then
  cp -R cs2-modded-server-${BRANCH}/custom_files/ ~/cs2_server/custom_files/
else
  cp -RT cs2-modded-server-${BRANCH}/custom_files/ ~/cs2_server/custom_files/
fi

echo "Merging in custom files from ${CUSTOM_FILES}"
cp -RT ~/cs2_server/${CUSTOM_FILES}/ ~/cs2_server/game/csgo/

cd ~/cs2_server

FILE="game/csgo/gameinfo.gi"
PATTERN="Game_LowViolence[[:space:]]*csgo_lv // Perfect World content override"
LINE_TO_ADD="\t\t\tGame\tcsgo/addons/metamod"
REGEX_TO_CHECK="^[[:space:]]*Game[[:space:]]*csgo/addons/metamod"

if ! grep -qE "$REGEX_TO_CHECK" "$FILE"; then
  awk -v pattern="$PATTERN" -v lineToAdd="$LINE_TO_ADD" '{
    print $0;
    if ($0 ~ pattern) {
      print lineToAdd;
    }
  }' "$FILE" > tmp_file && mv tmp_file "$FILE"
  echo "$FILE successfully patched for Metamod."
fi

rm -r ~/cs2_server-modded-server-${BRANCH} ~/${BRANCH}.zip

echo "Starting server on $PUBLIC_IP:$PORT"
./game/bin/linuxsteamrt64/cs2 \
  -dedicated \
  -console \
  -usercon \
  -autoupdate \
  -tickrate $TICKRATE \
  $IP_ARGS \
  -port $PORT \
  +map de_dust2 \
  +sv_visiblemaxplayers $MAXPLAYERS \
  -authkey $API_KEY \
  +sv_setsteamaccount $STEAM_ACCOUNT \
  +game_type 0 \
  +game_mode 0 \
  +mapgroup mg_active \
  +sv_lan $LAN \
  +sv_password $SERVER_PASSWORD \
  +rcon_password $RCON_PASSWORD \
  +exec $EXEC
