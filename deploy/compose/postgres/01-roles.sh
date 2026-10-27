#!/bin/sh
# Creates the migrator (owner) and app roles and the database on the first container start
# (data-model section 5, ADR-0012). Passwords come from secret files (compose) or env vars (tests).
set -eu

read_secret() {
  # $1 = env var holding the value, $2 = env var holding a file path
  eval "value=\${$1:-}"
  if [ -z "$value" ]; then
    eval "file=\${$2:-}"
    if [ -n "$file" ] && [ -f "$file" ]; then
      value=$(cat "$file")
    fi
  fi
  if [ -z "$value" ]; then
    echo "01-roles.sh: missing $1 / $2" >&2
    exit 1
  fi
  printf '%s' "$value"
}

MIGRATOR_PASSWORD=$(read_secret DEMOAPI_MIGRATOR_PASSWORD DEMOAPI_MIGRATOR_PASSWORD_FILE)
APP_PASSWORD=$(read_secret DEMOAPI_APP_PASSWORD DEMOAPI_APP_PASSWORD_FILE)
DB_NAME="${DEMOAPI_DB:-demoapi}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
  -v mig_pw="$MIGRATOR_PASSWORD" -v app_pw="$APP_PASSWORD" -v db="$DB_NAME" <<'SQL'
SET password_encryption = 'scram-sha-256';
CREATE ROLE demoapi_migrator LOGIN PASSWORD :'mig_pw' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
CREATE ROLE demoapi_app LOGIN PASSWORD :'app_pw' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
-- Database backstops (S-D7): the app never waits forever.
ALTER ROLE demoapi_app SET statement_timeout = '10s';
ALTER ROLE demoapi_app SET lock_timeout = '2s';
ALTER ROLE demoapi_app SET idle_in_transaction_session_timeout = '15s';
CREATE DATABASE :"db" OWNER demoapi_migrator;
REVOKE ALL ON DATABASE :"db" FROM PUBLIC;
GRANT CONNECT, TEMPORARY ON DATABASE :"db" TO demoapi_app;
SQL

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$DB_NAME" <<'SQL'
ALTER SCHEMA public OWNER TO demoapi_migrator;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
SQL

echo "01-roles.sh: roles demoapi_migrator and demoapi_app created"
