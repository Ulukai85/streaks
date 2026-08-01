# streaks

A personal streak tracker (add a challenge, see today's dashboard, tick it
off, see the streak). See `PROJECT-BRIEF.md` for full product/architecture
intent; `CLAUDE.md` maps the rest of the docs.

Stack: ASP.NET Core (.NET 10) + EF Core/PostgreSQL API in `api/`, Angular 22
frontend in `web/`, Docker Compose + Caddy deployment config in
`infrastructure/`.

## Running the backend stack (Docker Compose)

This brings up Postgres, the API, and Caddy — not the frontend (see "Local
development" below to run that).

```bash
cd infrastructure
cp .env.example .env   # adjust Postgres credentials if you want
docker compose up --build -d
```

`SEED_USER_NAME`/`SEED_USER_PASSWORD` in `.env` must be set before a fresh
Postgres volume can boot — the API refuses to seed the initial user (and
exits) without a `SEED_USER_PASSWORD` on an empty `Users` table (decision
#12, `docs/phase-3-plan.md`). No default ships in `.env.example` on purpose.

Three services come up on one Docker network:

| Service    | Image                | Host port | Purpose                                |
|------------|----------------------|-----------|-----------------------------------------|
| `postgres` | `postgres:18-alpine` | `5433`    | Database (mapped off 5432 — see below) |
| `api`      | built from `api/`    | —         | ASP.NET Core API, not exposed directly |
| `caddy`    | `caddy:2-alpine`     | `8080`    | Reverse proxy to `api`                 |

Verify it's up:

```bash
curl http://localhost:8080/api/health
# {"status":"ok","databaseConnected":true}
```

Tear down with `docker compose down` (add `-v` to also drop the Postgres
volume).

`infrastructure/docker-compose.prod.yml` is a server-only override that
points `api`/`caddy` at pre-built GHCR images instead of building locally —
see ADR 0010. It's never used in local dev; the commands above are
unaffected.

## Local development (without full containerization)

**Backend** — `cd api/Api && dotnet run`. Uses the connection string in
`appsettings.Development.json`, which points at `localhost:5433` — so the
compose Postgres must be running (`docker compose up -d postgres` is enough).

**Frontend** — `cd web && nvm use && npm start`. `proxy.conf.json` forwards
`/api` to `localhost:5154`, i.e. straight to the `dotnet run` instance above
— only Postgres needs to be running via compose for this loop.

## Project layout

- `api/` — backend (see `api/CLAUDE.md`)
- `web/` — frontend (see `web/CLAUDE.md`)
- `infrastructure/` — `docker-compose.yml`, `Caddyfile`, `.env.example`
- `docs/` — domain model, ADRs, conventions, troubleshooting (see root `CLAUDE.md`)
