#!/usr/bin/env bash
# Verified-restore drill — REFERENCE COPY ONLY.
#
# Live copy: /opt/streaks/backup/restore-drill.sh on the production VM. Not
# just a one-time proof: ADR 0011's Consequences call for re-running this
# periodically (monthly) so the "verified" claim doesn't go stale. Same
# deploy steps as backup.sh.
#
# Restores the latest restic snapshot into a throwaway Postgres container
# (never touches the real `postgres` container/volume) and prints row
# counts on the app's tables for a human to sanity-check against known
# production data. Requires RESTIC_REPOSITORY/RESTIC_PASSWORD/
# B2_ACCOUNT_ID/B2_ACCOUNT_KEY in the environment (source /opt/streaks/
# backup/.env first if running by hand).
set -euo pipefail

: "${RESTIC_REPOSITORY:?RESTIC_REPOSITORY not set}"
: "${RESTIC_PASSWORD:?RESTIC_PASSWORD not set}"

SNAPSHOT="${1:-latest}"
CONTAINER=streaks-restore-drill
IMAGE=postgres:18.4-alpine
DB=streaks_restore_drill
USER=drill
PASSWORD=drill

cleanup() {
  docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "==> Restoring snapshot '$SNAPSHOT' into scratch container '$CONTAINER'"

docker run -d --name "$CONTAINER" \
  -e POSTGRES_USER="$USER" -e POSTGRES_PASSWORD="$PASSWORD" -e POSTGRES_DB="$DB" \
  "$IMAGE" >/dev/null

echo "==> Waiting for scratch Postgres to accept connections"
until docker exec "$CONTAINER" pg_isready -U "$USER" -d "$DB" >/dev/null 2>&1; do
  sleep 1
done

echo "==> Streaming '$SNAPSHOT' from restic through pg_restore"
restic dump "$SNAPSHOT" streaks.dump \
  | docker exec -i "$CONTAINER" pg_restore -U "$USER" -d "$DB" --no-owner --exit-on-error

echo "==> Row counts (compare against known production data):"
SANITY_SQL="SELECT 'Users' AS \"table\", count(*) FROM \"Users\"
UNION ALL SELECT 'Challenges', count(*) FROM \"Challenges\"
UNION ALL SELECT 'Completions', count(*) FROM \"Completions\"
UNION ALL SELECT 'RefreshTokens', count(*) FROM \"RefreshTokens\";"
docker exec "$CONTAINER" psql -U "$USER" -d "$DB" -c "$SANITY_SQL"

echo "==> Drill complete — scratch container will be removed."
