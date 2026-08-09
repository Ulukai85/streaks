# Existing Problems

Known issues we've deliberately left unresolved, with the reasoning, so they
don't get re-investigated from scratch or mistaken for new regressions.

## `npm audit` reports 17 vulnerabilities in `web/` (6 moderate, 11 high)

**Symptom:** `npm audit` in `web/` reports vulnerabilities in `axios`,
`brace-expansion`, and `@hono/node-server`.

**Cause:** `@spartan-ng/cli` (see ADR 0006) supports both Angular CLI and Nx
workspaces, so it depends on the entire Nx toolchain (`nx`, `@nx/workspace`,
`@nx/js`, `@nx/angular`, etc.) even though this repo is a plain Angular CLI
workspace and never exercises Nx code paths. `nx@23.1.0` — the current
latest stable release — hard-pins `axios@1.16.1` and
`brace-expansion@5.0.6` as exact versions, both in vulnerable ranges. The
`@hono/node-server` advisory comes from a separate chain: `@angular/cli`
bundles an MCP server that depends on `@modelcontextprotocol/sdk`, which
depends on the vulnerable Hono adapter.

**Why left as-is:** all of this sits in `devDependencies`, only reachable
through Nx-specific code paths this repo never invokes — none of it reaches
the built app. `npm audit fix` cannot resolve it: there is no newer nx
release yet with patched pins, so nothing satisfies the constraint without
forcing. `npm audit fix --force` would downgrade `@angular/cli` to 21.0.4 to
resolve the Hono chain, which is a larger regression than the vulnerability
it fixes.

**Revisit when:** Nx or spartan-ng cut a release with updated `axios`/
`brace-expansion` pins (re-run `npm audit` in `web/` periodically), or if we
start actually using Nx-specific tooling, at which point the risk
calculation changes.

**Rejected fix:** adding an `overrides` entry in `web/package.json` to force
patched `axios`/`brace-expansion` versions was considered — it would
override an exact pin `nx` explicitly declared, with unclear compatibility
consequences for a package we don't meaningfully exercise. Not worth it for
a dev-only, unreachable vulnerability.

**Dependabot confirmation (2026-08-02):** after enabling Dependabot, it
reached the same conclusion independently. It auto-opened a fix PR for
`axios`, but reported `@hono/node-server` and `brace-expansion` as
non-auto-fixable for the reasons already documented above (the only
available paths require downgrading `@angular/cli` from 22.0.8 to 21.0.4,
or updating `@spartan-ng/cli`'s pinned Nx/`minimatch` chain). Both alerts
were dismissed on GitHub as tolerable risk, pointing back to this entry.
Earliest fixed versions per Dependabot, for reference when revisiting:
`@hono/node-server@2.0.10`, `brace-expansion@2.1.3`.

## Server infra config files aren't synced by CI/CD

**Symptom:** a change to `infrastructure/docker-compose.yml` (or
`Caddyfile`, `docker-compose.prod.yml`, `.env.example`) merged and deployed
via CD has zero effect on the running containers — the server keeps running
whatever those files looked like the last time someone copied them over by
hand.

**Cause:** per ADR 0010, the production VM's `/opt/streaks/infrastructure/`
isn't a git checkout — it's four files (`docker-compose.yml`,
`docker-compose.prod.yml`, `Caddyfile`, `.env`) that `deploy.sh` and the
CD pipeline never touch. CD only pulls new `api`/`caddy` images and runs
`docker compose up -d`; it doesn't `scp` or otherwise sync these files from
the repo. Concretely hit during Phase 5 Stage B: `infrastructure/
docker-compose.yml` gained `OTEL_EXPORTER_OTLP_ENDPOINT`/`OTEL_SERVICE_NAME`
env vars and an `extra_hosts` entry for the API's Grafana Alloy wiring, and
none of it took effect until the file was manually `scp`'d to the server
and `docker compose up -d` was re-run there — with no error or warning
anywhere in CI/CD to indicate the drift.

**Why left as-is:** these four files are already the minimal, deliberately
human-reviewed surface for anything that touches the production host
directly (secrets in `.env`, TLS/proxy config in `Caddyfile`) — auto-syncing
them from CI/CD would mean unreviewed infra changes land and take effect on
every merge to `main`, which is a bigger risk than the current "someone has
to remember to copy the file" gap, especially pre-Stage-D (no verified
backup/restore yet to fall back on if an auto-synced compose change breaks
something).

**Revisit when:** this bites again, or once Phase 5 Stage D (backups with a
verified restore) is done and an automated-but-reviewed sync (e.g. a CD step
that diffs the four files and fails the deploy with a warning if they've
drifted, rather than silently applying them) becomes a reasonable tradeoff.
In the meantime: after merging any `infrastructure/` change other than the
`api`/`caddy` image tags, manually copy the changed file(s) to
`/opt/streaks/infrastructure/` on the server and re-run `docker compose
up -d` there before assuming the change is live.
