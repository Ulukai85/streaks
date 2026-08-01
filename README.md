# streaks

A personal streak tracker (add a challenge, see today's dashboard, tick it
off, see the streak). See `PROJECT-BRIEF.md` for full product/architecture
intent; `CLAUDE.md` maps the rest of the docs.

Stack: ASP.NET Core (.NET 10) + EF Core/PostgreSQL API in `api/`, Angular 22
frontend in `web/`, Docker Compose + Caddy deployment config in
`infrastructure/`.

## Running the full stack (Docker Compose)

This brings up Postgres, the API, and Caddy — Caddy serves the built
Angular app itself and reverse-proxies `/api/*` to the API, so this is the
whole app, not just the backend (see "Local development" below for a
faster edit-reload loop instead).

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

| Service    | Image                       | Host port         | Purpose                                                |
|------------|------------------------------|-------------------|---------------------------------------------------------|
| `postgres` | `postgres:18.4-alpine`      | `127.0.0.1:5433`  | Database (loopback-only — not reachable off the box)   |
| `api`      | built from `api/`           | —                 | ASP.NET Core API, not exposed directly                 |
| `caddy`    | built from `web/`           | `8080`            | Serves the built Angular app + reverse-proxies `/api/*` |

All three run as non-root inside their containers. Verify it's up:

```bash
curl http://localhost:8080/api/health
# {"status":"ok","databaseConnected":true}
curl -I http://localhost:8080/
# 200 OK — the Angular app shell
```

Tear down with `docker compose down` (add `-v` to also drop the Postgres
volume).

## Production deployment

CI (`.github/workflows/ci.yml`) tests and builds every PR and push to
`main`; CD (`.github/workflows/deploy.yml`) then builds `api`/`caddy`
images, pushes them to GHCR, and deploys over SSH once CI is green — see
`docs/decisions/0010-deploy-mechanism.md` for the full decision and
`docs/phase-4-plan.md` (Stage 6) for the current setup status.

The server does **not** need a clone of this repo — only four files under
an `infrastructure/` directory (e.g. `/opt/streaks/infrastructure/`):
`docker-compose.yml`, `docker-compose.prod.yml` (a server-only override
pointing `api`/`caddy` at the pre-built GHCR images instead of building
locally — never used in local dev), `Caddyfile`, and a real `.env`. CD only
pulls/restarts images; it does not sync these four files, so a change to
any of them needs manually re-copying to the server.

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
- `infrastructure/` — `docker-compose.yml`, `docker-compose.prod.yml`,
  `Caddyfile`, `deploy.sh`, `.env.example`
- `.github/workflows/` — CI (`ci.yml`) and CD (`deploy.yml`)
- `docs/` — domain model, ADRs, conventions, troubleshooting (see root `CLAUDE.md`)
