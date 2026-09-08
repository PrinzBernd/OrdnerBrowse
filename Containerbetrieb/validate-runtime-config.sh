#!/bin/sh
set -eu

fail()
{
    printf '%s\n' "[f] Ungültige Container-Laufzeitkonfiguration: $1" >&2
    exit 1
}

validate_positive_decimal_id()
{
    name="$1"
    value="$2"

    case "$value" in
        ''|0|0[0-9]*|*[!0-9]*)
            fail "$name muss eine positive dezimale Ganzzahl ohne Vorzeichen, Leerzeichen oder führende Null sein."
            ;;
    esac
}

validate_positive_decimal_id "APP_UID" "${APP_UID-}"
validate_positive_decimal_id "APP_GID" "${APP_GID-}"

printf '%s\n' '[x] APP_UID und APP_GID sind gültige positive dezimale IDs.'
