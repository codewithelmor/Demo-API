#!/bin/sh
# Generates DEV-ONLY secrets for docker compose on first run (ADR-0012, PRD P4).
# Runs as root in the one-shot `secrets-init` container with the `dev-secrets` volume at /secrets.
#
# Layout (one folder per consuming service; each service mounts only its own folder, read-only):
#   .generated/  root 0700  source values (never overwritten unless a .env override is set)
#   api/         uid 1654   key-per-file config: Jwt__SigningKey, SeedAdmin__Password,
#                           ConnectionStrings__Postgres (app role), ConnectionStrings__Valkey
#   migrator/    uid 1654   pgpass for demoapi_migrator (Npgsql Passfile=)
#   bff/         uid 1654   key-per-file config for the web BFF: Valkey__ConnectionString (user demoapi_bff)
#   postgres/    uid 999    POSTGRES_PASSWORD, DEMOAPI_MIGRATOR_PASSWORD, DEMOAPI_APP_PASSWORD
#   valkey/      uid 999    users.acl (SHA-256 hashed passwords), password (for the healthcheck)
# Files are mode 0440. Changing DB passwords after the first start needs `docker compose down -v`.
set -eu
umask 077

ROOT=/secrets
SRC="$ROOT/.generated"
DB_NAME="${DEMOAPI_DB:-demoapi}"
APP_UID=1654      # non-root user of the .NET chiseled images (api, migrator)
PG_UID=999        # postgres:18.6-trixie
PG_GID=999
VALKEY_UID=999    # valkey/valkey:9.1.2-alpine
VALKEY_GID=1000

log() { echo "secrets-init: $*"; }
fail() { echo "secrets-init: $*" >&2; exit 1; }

random_password() { # 40 alphanumeric characters (~238 bits); safe in connection strings, pgpass and ACL files
  tr -dc 'A-Za-z0-9' </dev/urandom | head -c 40
}

random_key() { # 64 random bytes, base64 (Jwt:SigningKey must be base64 with >= 256 bits)
  head -c 64 /dev/urandom | base64 | tr -d '\n'
}

check_password_override() { # $1 = name, $2 = value; values end up in connection strings and pgpass
  case "$2" in
    *[!A-Za-z0-9._~-]*) fail "$1 override may only contain A-Z a-z 0-9 . _ ~ -" ;;
  esac
  [ "${#2}" -ge 16 ] || fail "$1 override must be at least 16 characters"
}

check_key_override() {
  bytes=$(printf '%s' "$2" | base64 -d 2>/dev/null | wc -c) || bytes=0
  [ "$bytes" -ge 32 ] || fail "$1 override must be base64 with at least 32 bytes"
}

# ensure NAME OVERRIDE GENERATOR: keeps an existing value; an override from .env replaces it.
ensure() {
  file="$SRC/$1"
  if [ -n "$2" ]; then
    if [ ! -f "$file" ] || [ "$(cat "$file")" != "$2" ]; then
      printf '%s' "$2" >"$file"
      log "$1 set from .env override"
    fi
  elif [ ! -s "$file" ]; then
    "$3" >"$file"
    log "$1 generated"
  fi
  chmod 0400 "$file"
}

# put DIR NAME UID GID VALUE: (re)writes a derived file atomically with mode 0440.
put() {
  tmp="$1/.$2.tmp"
  printf '%s' "$5" >"$tmp"
  chown "$3:$4" "$tmp"
  chmod 0440 "$tmp"
  mv -f "$tmp" "$1/$2"
}

dir() { # dir PATH UID GID
  mkdir -p "$1"
  chown "$2:$3" "$1"
  chmod 0550 "$1"
}

mkdir -p "$SRC"
chmod 0700 "$SRC"

[ -z "${JWT_SIGNING_KEY:-}" ] || check_key_override JWT_SIGNING_KEY "$JWT_SIGNING_KEY"
[ -z "${POSTGRES_PASSWORD:-}" ] || check_password_override POSTGRES_PASSWORD "$POSTGRES_PASSWORD"
[ -z "${POSTGRES_APP_PASSWORD:-}" ] || check_password_override POSTGRES_APP_PASSWORD "$POSTGRES_APP_PASSWORD"
[ -z "${VALKEY_PASSWORD:-}" ] || check_password_override VALKEY_PASSWORD "$VALKEY_PASSWORD"
if [ -n "${SEED_ADMIN_PASSWORD:-}" ] && [ "${#SEED_ADMIN_PASSWORD}" -lt 15 ]; then
  fail "SEED_ADMIN_PASSWORD override must be at least 15 characters"
fi

ensure jwt_signing_key "${JWT_SIGNING_KEY:-}" random_key
ensure seed_admin_password "${SEED_ADMIN_PASSWORD:-}" random_password
ensure postgres_password "${POSTGRES_PASSWORD:-}" random_password
ensure migrator_password "" random_password
ensure app_password "${POSTGRES_APP_PASSWORD:-}" random_password
ensure valkey_password "${VALKEY_PASSWORD:-}" random_password
ensure bff_valkey_password "" random_password

JWT_KEY=$(cat "$SRC/jwt_signing_key")
ADMIN_PW=$(cat "$SRC/seed_admin_password")
PG_PW=$(cat "$SRC/postgres_password")
MIG_PW=$(cat "$SRC/migrator_password")
APP_PW=$(cat "$SRC/app_password")
VK_PW=$(cat "$SRC/valkey_password")
VK_HASH=$(printf '%s' "$VK_PW" | sha256sum | cut -d ' ' -f 1)
BFF_VK_PW=$(cat "$SRC/bff_valkey_password")
BFF_VK_HASH=$(printf '%s' "$BFF_VK_PW" | sha256sum | cut -d ' ' -f 1)

# postgres: superuser + role passwords for 01-roles.sh
dir "$ROOT/postgres" "$PG_UID" "$PG_GID"
put "$ROOT/postgres" POSTGRES_PASSWORD "$PG_UID" "$PG_GID" "$PG_PW"
put "$ROOT/postgres" DEMOAPI_MIGRATOR_PASSWORD "$PG_UID" "$PG_GID" "$MIG_PW"
put "$ROOT/postgres" DEMOAPI_APP_PASSWORD "$PG_UID" "$PG_GID" "$APP_PW"

# valkey: default user off; app user limited to products:* keys (plus HybridCache's own __MSFT_HCT__ tag keys, which every
# read checks; without them each L2 call fails with NOPERM) and no @dangerous commands
dir "$ROOT/valkey" "$VALKEY_UID" "$VALKEY_GID"
put "$ROOT/valkey" users.acl "$VALKEY_UID" "$VALKEY_GID" "user default reset off
user demoapi reset on #$VK_HASH ~products:* ~__MSFT_HCT__* resetchannels +@all -@dangerous
user demoapi_bff reset on #$BFF_VK_HASH ~bff:* resetchannels +@all -@dangerous
"
put "$ROOT/valkey" password "$VALKEY_UID" "$VALKEY_GID" "$VK_PW"

# migrator: pgpass only (hostname:port:database:username:password)
dir "$ROOT/migrator" "$APP_UID" "$APP_UID"
put "$ROOT/migrator" pgpass "$APP_UID" "$APP_UID" "postgres:5432:$DB_NAME:demoapi_migrator:$MIG_PW
"

# api: key-per-file configuration read from /run/secrets/dev (Program.cs AddKeyPerFile)
dir "$ROOT/api" "$APP_UID" "$APP_UID"
put "$ROOT/api" Jwt__SigningKey "$APP_UID" "$APP_UID" "$JWT_KEY"
put "$ROOT/api" SeedAdmin__Password "$APP_UID" "$APP_UID" "$ADMIN_PW"
put "$ROOT/api" ConnectionStrings__Postgres "$APP_UID" "$APP_UID" \
  "Host=postgres;Port=5432;Database=$DB_NAME;Username=demoapi_app;Password=$APP_PW;GSS Encryption Mode=Disable"
put "$ROOT/api" ConnectionStrings__Valkey "$APP_UID" "$APP_UID" \
  "valkey:6379,user=demoapi,password=$VK_PW,abortConnect=false,tiebreaker=,configChannel=,version=7.2"

# bff: key-per-file configuration for the web BFF, read from /run/secrets/bff (BffHost.AddBff)
dir "$ROOT/bff" "$APP_UID" "$APP_UID"
put "$ROOT/bff" Valkey__ConnectionString "$APP_UID" "$APP_UID" \
  "valkey:6379,user=demoapi_bff,password=$BFF_VK_PW,abortConnect=false,tiebreaker=,configChannel=,version=7.2"

log "dev secrets ready (values not printed)"
