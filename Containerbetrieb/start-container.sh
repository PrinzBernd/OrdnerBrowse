#!/bin/sh
set -eu

fail()
{
    printf '%s\n' "[f] Containerstart abgebrochen: $1" >&2
    exit 1
}

if [ "$#" -ne 1 ]; then
    fail "genau ein Compose-Projektname muss angegeben werden."
fi

PROJECT_NAME="$1"

case "$PROJECT_NAME" in
    ''|[!A-Za-z0-9]*|*[!A-Za-z0-9_.-]*)
        fail "der Compose-Projektname enthält unzulässige Zeichen."
        ;;
esac

SCRIPT_DIR="$(
    CDPATH= cd -- "$(dirname -- "$0")" &&
    pwd
)" || fail "Skriptverzeichnis konnte nicht bestimmt werden."

ENV_FILE="$SCRIPT_DIR/.env"
COMPOSE_FILE="$SCRIPT_DIR/compose.yaml"
VALIDATOR="$SCRIPT_DIR/validate-runtime-config.sh"

[ -f "$ENV_FILE" ] ||
    fail ".env fehlt."
[ -f "$COMPOSE_FILE" ] ||
    fail "compose.yaml fehlt."
[ -f "$VALIDATOR" ] ||
    fail "validate-runtime-config.sh fehlt."

set -a
# .env enthält im freigegebenen Modell ausschließlich nicht geheime Laufzeitwerte.
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

/bin/sh "$VALIDATOR" ||
    fail "APP_UID/APP_GID-Validierung fehlgeschlagen."

DOCKER_BIN="$(command -v docker || true)"
[ -n "$DOCKER_BIN" ] ||
    fail "docker wurde nicht gefunden."

"$DOCKER_BIN" compose \
    --env-file "$ENV_FILE" \
    -p "$PROJECT_NAME" \
    -f "$COMPOSE_FILE" \
    up -d

"$DOCKER_BIN" compose \
    --env-file "$ENV_FILE" \
    -p "$PROJECT_NAME" \
    -f "$COMPOSE_FILE" \
    ps

printf '%s\n' '[x] Freigegebener Containerstart und Statusprüfung erfolgreich.'
