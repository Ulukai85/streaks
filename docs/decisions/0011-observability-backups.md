# ADR 0011 — Observability, error tracking, and backup toolchain

Date: 2026-08-02
Status: Accepted

## Context

Phase 4 shipped the app to a small Hetzner VM (≤2 vCPU/4GB) with no
logging/monitoring/backup story beyond a hand-rolled `/api/health` endpoint
and Docker's default container logging. `PROJECT-BRIEF.md` §11 assigns Phase
5 "Heatmap, OTel, PWA manifest, backups with a verified restore," but gives
no tool-level detail, and no prior ADR touches logging, monitoring,
telemetry, or backups. `docs/phase-4-plan.md`'s Stage 4 preconditions
already flagged the risk to avoid: an unverified `pg_dump` cron job "under
the assumption it's better than nothing."

This ADR bundles the observability/error-tracking/backup tool choices into
one decision because they share a single constraining fact and a single
Rationale: the VM is small enough that a full self-hosted stack (Loki +
Prometheus + Tempo + Grafana, or an all-in-one like SigNoz — 2–4GB+ RAM on
their own per current research) would compete directly with Postgres, the
API, and Caddy already running there. Every choice below follows from
offloading the heavy storage/query workload to managed free tiers and
keeping only thin collectors on the box itself.

## Decision

- **Metrics, logs, and traces ship to Grafana Cloud's free tier** via
  **Grafana Alloy** — a single-binary collector (successor to Grafana
  Agent/Promtail) running directly on the host (not inside
  `infrastructure/docker-compose.yml`, since it needs to read host log
  files, `journald`, and `docker logs` across the whole box, not just the
  Compose network — the same reasoning that already put the domain-facing
  Caddy outside Compose in Phase 4 Stage 2). One collector process, one
  free-tier allowance, covering:
  - The API's own OpenTelemetry export (traces + metrics + logs).
  - `api`/`caddy`/`postgres` container stdout/stderr.
  - The host-level Caddy's access/error log.
  - `fail2ban`, `ufw`, `unattended-upgrades` logs.
  - `auth.log`/`sshd` (journald) — SSH login attempts, complementing
    `fail2ban`'s ban log with the underlying attempt log.
  - The compose-internal `Caddyfile` gains a `log` block (currently absent —
    only default behavior applies today), so its access log is actually
    structured and collectible rather than whatever the default emits.
- **Application instrumentation: OpenTelemetry .NET SDK** (`OpenTelemetry.
  Extensions.Hosting` + `Instrumentation.AspNetCore` + `Instrumentation.Http`
  + Npgsql instrumentation), wired via the single-call `UseOtlpExporter()`
  pattern, exporting to Alloy over `localhost:4317`. No separate logging
  library (e.g. Serilog) is added — `Microsoft.Extensions.Logging` already
  emits structured logs, and OTel's logging bridge exports those same
  records through the same pipeline instead of running two logging systems
  side by side.
- **Error tracking: Sentry SaaS free tier** (5,000 events/month, 1 user,
  30-day retention) via Sentry's .NET and Angular SDKs, sending directly to
  Sentry's cloud — independent of the Alloy/Grafana pipeline.
- **Backups: `restic` → Backblaze B2.** A `pg_dump` logical backup (not raw
  volume files, so it's a consistent point-in-time snapshot independent of
  Postgres's on-disk layout) is piped through `restic` to a B2 bucket
  (native S3-compatible support, 10GB free) on a systemd timer (preferred
  over cron on a VPS: cleaner logs, more predictable behavior across
  reboots). **A verified-restore drill is part of the deliverable, not an
  afterthought** — an unrestored backup isn't a backup; the implementation
  must include an actual `pg_restore` test against a scratch database, run
  and documented, not just a scheduled dump job left untested.
- **Uptime monitoring: a free external SaaS checker** (UptimeRobot or Better
  Stack free tier), not self-hosted — a monitor co-located with the box it
  watches dies with the box, so this has to run off-box regardless of any
  other self-hosting preference.

## Rationale

- **Managed free tier over self-hosting the storage/query layer** because
  the VM's headroom is the binding constraint: a self-hosted LGTM stack or
  SigNoz needs more RAM on its own than this box has total, before counting
  what Postgres/api/Caddy already use. Grafana Cloud's free allowances are
  generous enough for a single-user app's volume, and keeps the Grafana UI
  the user already knows without taking on that operational weight.
- **Alloy as the one local collector** rather than separate agents per
  signal type (the old Agent/Promtail/per-exporter pattern) because a single
  binary with one config file is strictly less to run and maintain on an
  already-constrained box, and it natively handles metrics, logs, and
  traces together.
- **Alloy on the host, not in Compose** because its job is to watch the
  whole box (host log files, `journald`, the Docker socket for all three
  containers' logs) — scoping it inside the Compose network the way the
  compose-internal Caddy is scoped would cut it off from host-level Caddy,
  fail2ban, ufw, and SSH logs entirely.
- **Sentry SaaS over self-hosting** because official self-hosted Sentry runs
  40+ containers — even lighter Sentry-DSN-compatible alternatives
  (GlitchTip, Bugsink) add operational surface that a single-user app's
  error volume (well under the free tier's 5,000/month) doesn't justify
  taking on.
- **No separate logging library** because adding Serilog (or similar) on
  top of `Microsoft.Extensions.Logging` plus OTel's own logging bridge would
  be two logging pipelines doing the same job; the built-in logging
  abstraction already produces structured log records, and OTel exports
  those as-is.
- **`restic` over Borg** because `restic`'s native S3/B2 support avoids an
  SSH-based backend Borg would otherwise need, and current comparisons favor
  its restore speed and simpler key management for exactly this "one small
  VPS, one cloud bucket" shape — Borg's superior compression matters more at
  a scale this app doesn't have.
- **`pg_dump` over raw volume/filesystem backup** because a logical dump is
  restorable independent of the exact Postgres minor version or on-disk
  format running at restore time, and is the natural fit for `pg_restore`-
  based verification.
- **A mandatory, executed verified-restore drill** because this is the exact
  gap Phase 4 flagged when it deferred backups here in the first place — the
  brief's own phrasing ("with a verified restore") makes this non-optional,
  not a nice-to-have to skip if time runs short.
- **External free SaaS uptime checker, no exception for self-hosting
  preference** because Uptime Kuma (or any self-hosted equivalent) run on
  the same VM it's checking cannot report an outage of that same VM — this
  one piece has to be off-box by construction, independent of every other
  tool choice above.

## Consequences

- Telemetry data (traces, metrics, logs, error events) leaves the VM and is
  stored on Grafana Cloud's and Sentry's infrastructure rather than staying
  fully on-box — accepted in exchange for not needing to run and maintain a
  multi-GB observability stack on a small VM. Revisit if data residency
  requirements ever change (there are none today — single-user personal
  app).
- A new host-level process (Alloy) needs installing and keeping running on
  the server, in the same "human-owned, outside Compose" category as the
  host-level Caddy and `deploy.sh` — not something CI/CD touches.
- Free-tier allowances (Grafana Cloud, Sentry, B2, the uptime checker) are
  not contractually guaranteed to stay free or at the same limits forever;
  if usage or pricing changes make them non-viable, the collector-based
  architecture (Alloy, OTel SDK, restic) doesn't need to change — only the
  destination each ships to would, since none of the on-box tooling is
  tied to a specific paid backend.
- The verified-restore drill is a one-time proof at implementation time, not
  a standing guarantee — it should be re-run periodically (research
  suggests structural checks weekly, full restore verification monthly) or
  after any Postgres major-version bump, or the "verified" claim goes stale.
- No dedicated logging library (Serilog etc.) means log enrichment/
  formatting is whatever `Microsoft.Extensions.Logging` + the OTel bridge
  support out of the box — acceptable today; revisit only if a concrete gap
  shows up in practice.
