#!/usr/bin/env bash
# Backup process — REFERENCE COPY ONLY.
#
# The live copy this runs from is /opt/streaks/backup/backup.sh on the
# production VM, invoked by streaks-backup.service (systemd, not Compose —
# see ADR 0011 and infrastructure/backup/.env.example). Not synced by
# CI/CD; edit here, then:
#
#   scp infrastructure/backup/backup.sh deploy@<host>:/tmp/backup.sh
#   ssh deploy@<host> 'sudo install -m 755 /tmp/backup.sh /opt/streaks/backup/backup.sh'
#
# pg_dump runs inside the `postgres` container (via `docker compose exec`,
# resolved against the compose project at /opt/streaks/infrastructure — see
# deploy.sh) so it always matches the server's actual Postgres version,
# rather than needing a host-installed pg_dump kept in lockstep by hand.
# Custom format (-Fc) instead of plain SQL: smaller, and restorable
# selectively/in parallel via pg_restore.
set -euo pipefail

cd /opt/streaks/infrastructure

: "${RESTIC_REPOSITORY:?RESTIC_REPOSITORY not set}"
: "${RESTIC_PASSWORD:?RESTIC_PASSWORD not set}"
: "${POSTGRES_USER:?POSTGRES_USER not set}"
: "${POSTGRES_DB:?POSTGRES_DB not set}"

docker compose exec -T postgres pg_dump -U "$POSTGRES_USER" -Fc "$POSTGRES_DB" \
  | restic backup --stdin --stdin-filename streaks.dump

restic forget --prune --keep-daily 7 --keep-weekly 4 --keep-monthly 6
