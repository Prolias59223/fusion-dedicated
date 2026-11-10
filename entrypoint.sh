#!/bin/bash
# Entrypoint standard Pterodactyl : remplace {{VAR}} dans STARTUP puis l'exécute
cd /home/container || exit 1
export HOME=/home/container
PARSED=$(echo "${STARTUP}" | sed -e 's/{{/${/g' -e 's/}}/}/g' | eval echo "$(cat -)")
printf "\033[1m\033[33mcontainer@pterodactyl~ \033[0m%s\n" "$PARSED"
# shellcheck disable=SC2086
exec env ${PARSED}
