#!/usr/bin/env bash
set -euo pipefail

cd /opt/streaks/infrastructure

docker compose -f docker-compose.yml -f docker-compose.prod.yml pull
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --remove-orphans
