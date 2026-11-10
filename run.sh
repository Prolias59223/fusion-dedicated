#!/bin/bash
# Lance Xvfb -> Steam -> Fusion Dedicated (ou mode SETUP avec VNC pour la 1re connexion Steam)
export HOME=/home/container
export DISPLAY=:99
APP=/home/container/server
SETUP_MODE="${SETUP_MODE:-0}"
VNC_PORT="${VNC_PORT:-5900}"
STEAM_WAIT="${STEAM_WAIT:-60}"

cd /home/container || exit 1

if [ ! -f "$APP/fusiondedicated.dll" ]; then
  echo "[egg] fusiondedicated.dll introuvable dans $APP : relancez l'installation (Reinstall)."
  exit 1
fi

# libsteam_api.so : à déposer par vous (SDK Steamworks) dans /home/container ou /home/container/server
if [ -f /home/container/libsteam_api.so ] && [ ! -f "$APP/libsteam_api.so" ]; then
  cp /home/container/libsteam_api.so "$APP/libsteam_api.so"
fi
if [ ! -f "$APP/libsteam_api.so" ]; then
  echo "[egg] libsteam_api.so manquant. Récupérez-le dans le SDK Steamworks"
  echo "[egg] (redistributable_bin/linux64/libsteam_api.so) et uploadez-le dans le File Manager."
  exit 1
fi

# --- Affichage virtuel ---
rm -f /tmp/.X99-lock /tmp/.X11-unix/X99 2>/dev/null
Xvfb :99 -screen 0 1280x800x24 -nolisten tcp >/home/container/xvfb.log 2>&1 &
sleep 2

STEAM_ARGS="-no-cef-sandbox -cef-disable-gpu"

if [ "$SETUP_MODE" = "1" ]; then
  echo "[egg] MODE SETUP : connexion Steam via VNC sur le port $VNC_PORT (mot de passe = VNC_PASSWORD)."
  echo "[egg] Connectez-vous avec le compte Steam DEDIE (SteamVR ajouté), validez Steam Guard,"
  echo "[egg] puis mettez SETUP_MODE=0 et redémarrez."
  openbox >/home/container/openbox.log 2>&1 &
  x11vnc -display :99 -forever -shared -rfbport "$VNC_PORT" -passwd "${VNC_PASSWORD:-changeme}" -o /home/container/x11vnc.log -bg
  steam $STEAM_ARGS >/home/container/steam.log 2>&1 &
  STEAM_PID=$!
  trap 'kill $STEAM_PID 2>/dev/null; pkill -u "$(id -u)" Xvfb; exit 0' INT TERM
  wait $STEAM_PID
  exit 0
fi

# --- Mode normal ---
echo "[egg] Démarrage de Steam (silencieux)..."
steam -silent $STEAM_ARGS >/home/container/steam.log 2>&1 &
STEAM_PID=$!

for i in $(seq 1 "$STEAM_WAIT"); do
  [ -p "$HOME/.steam/steam.pipe" ] && break
  sleep 1
done
sleep 15

cd "$APP" || exit 1
export LD_LIBRARY_PATH="$APP:$LD_LIBRARY_PATH"
echo "[egg] Démarrage de Fusion Dedicated..."
dotnet fusiondedicated.dll &
SERVER_PID=$!

cleanup() {
  kill -INT "$SERVER_PID" 2>/dev/null
  wait "$SERVER_PID" 2>/dev/null
  kill "$STEAM_PID" 2>/dev/null
  pkill -u "$(id -u)" Xvfb 2>/dev/null
  exit 0
}
trap cleanup INT TERM

wait "$SERVER_PID"
CODE=$?
kill "$STEAM_PID" 2>/dev/null
pkill -u "$(id -u)" Xvfb 2>/dev/null
exit $CODE
