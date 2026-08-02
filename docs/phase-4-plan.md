# Phase 4 — Ship to Hetzner

> Working plan for Phase 4 (see `PROJECT-BRIEF.md` §11: *"Ship to Hetzner:
> Compose, Caddy, GitHub Actions on push to main. Then use it daily for two
> weeks."*). Tracks stage status as work lands.

## Context

Phases 1–3 produced a working app with no real deployment story: `infrastructure/`
brings up Postgres + the API + Caddy via Compose, but Caddy only reverse-proxies
`/api/*` on plain HTTP port 8080 — there is no served frontend, no domain, no
TLS, and no CI/CD. `ADR 0008`'s token-storage decision has a load-bearing
**Precondition** that Phase 4 has to actually deliver: *"Caddy serves the built
Angular app and reverse-proxies `/api/*` to the backend under one domain. If the
frontend is ever served from a different origin than the API, this ADR must be
revisited."* `SameSite=Strict` on the refresh cookie is not actually safe until
that same-origin shape exists in production — so this phase isn't just ops
plumbing, it closes out a security assumption Phase 3 shipped ahead of.

Two things fall outside this phase on purpose: backups (Phase 5, "with a
verified restore" — don't improvise a partial version now) and registration UI
(still out of scope per Phase 3 decision #3 — the seeded single account is
still how this app gets a user in production).

**Carried-over preconditions (do not re-litigate):**

1. **Same-origin topology is mandatory, not a nice-to-have.** Caddy serves the
   Angular build as static files and reverse-proxies `/api/*` to the backend
   under one domain — this is what makes `SameSite=Strict` (ADR 0008) actually
   hold in production.
2. **Backups are explicitly out of scope this phase.** Phase 5 owns backups
   *with a verified restore*; do not add an unverified `pg_dump` cron job now
   under the assumption it's "better than nothing" — track it as a Phase 5 item
   instead.
3. **No registration UI, no multi-account support.** The seeded account
   (Phase 3 decision #3/#6) remains the only way this app gets a user.

**Ordering:** infrastructure/app changes first (Stages 1–4), then the
human-owned server-hardening pass (Stage 5), then CI/CD (Stage 6), then the
two-week dogfooding close-out (Stage 7). Each numbered stage is one reviewable
diff; stop after each for review — except Stage 5, which isn't a diff at all
(see that stage).

---

## Stage 1 — Same-origin serving: Caddy serves the Angular build

**Status: Done**

- Add a multi-stage `web/Dockerfile`: a `node` build stage running `npm ci && npm run build`,
  then copy the built `dist/streaks/browser` output into a stage Caddy can serve
  from (either directly into a `caddy:2-alpine` image, or a shared volume
  populated by a build-only container — pick whichever keeps the Caddy
  container itself unchanged from upstream `caddy:2-alpine`).
- Update `infrastructure/Caddyfile`: `file_server` the Angular build with an
  SPA fallback to `index.html` (client-side routing needs this — `/challenges`
  hit directly must still serve the app shell), plus `handle_path /api/*`
  (or equivalent) reverse-proxying to `api:8080` exactly as today.
- Update `infrastructure/docker-compose.yml` to build/mount the new `web`
  stage into Caddy.
- No ADR needed — this implements ADR 0008's existing precondition, it
  doesn't make a new architectural choice.

**Verify:** `docker compose up --build`, hit Caddy's port directly: `/`
serves the Angular shell, a deep link like `/challenges` (typed directly, not
navigated from `/`) still serves the shell instead of a Caddy 404, `/api/health`
still proxies through. Confirm the refresh cookie is now attached correctly
on a same-origin login from the browser (it was already same-origin in dev via
the Angular proxy, but this is the first time it's same-origin through Caddy).

---

## Stage 2 — Domain + TLS

**Status: Done (human).** Domain live, host-level Caddy reverse-proxying
`localhost:8080`, Let's Encrypt cert issued. Verified end-to-end over the
real domain: `curl -v https://<domain>/api/health` shows a valid cert chain,
plain-HTTP requests redirect to HTTPS, and — the one thing Stage 1's
plain-`curl`-against-localhost testing couldn't prove — the refresh cookie is
confirmed `Secure` in the browser and correctly sent back on subsequent
requests over HTTPS.

Stage 1 confirmed the actual production topology: a **host-level Caddy**
(outside Docker, not part of this repo) fronts the Hetzner box's public
80/443 and reverse-proxies the real domain to `localhost:8080`, where the
compose `caddy` service (this repo) serves the Angular build and proxies
`/api/*`. `infrastructure/docker-compose.yml` already maps the compose Caddy
to host port 8080, not 80/443 — this was already anticipated, not something
Stage 2 needs to change.

That makes this stage's shape the same as Stage 5: server/DNS work the human
does directly, recorded here (with the "why") so it doesn't need
re-litigating.

1. **TLS termination and ACME live at the host-level Caddy**, not the
   compose `caddy` service — the host Caddy already reverse-proxies other
   traffic on the box and owns 80/443; the compose Caddy keeps listening on
   plain `:80` internally (mapped to host `8080`), reachable only via the
   host Caddy's `reverse_proxy localhost:8080`.
2. **`infrastructure/Caddyfile` requires no change** for this stage — it
   doesn't need to know the domain name at all; only the host Caddy config
   does.
3. **The refresh cookie's `Secure` flag needs no code change.**
   `AuthEndpoints.cs`'s `Secure = !env.IsDevelopment()` is already
   unconditional in Production — confirmed directly via `curl` against the
   Stage 1 deployment, which showed `secure` on the `Set-Cookie` header over
   plain HTTP. What's left is an end-to-end verification once the real
   domain is live: a browser won't send a `Secure` cookie back over plain
   HTTP, so this can only be fully confirmed against the real domain, not
   `curl` against localhost.
4. **Server-side steps** (human does directly): point the domain's
   `A`/`AAAA` record at the Hetzner IP; add a block to the host Caddyfile
   (`your-domain.example { reverse_proxy localhost:8080 }`); reload/restart
   the host Caddy; confirm Let's Encrypt issues a cert.

**Verify:** `curl -v https://<domain>/api/health` shows a valid cert chain;
`curl -v http://<domain>/` redirects to `https://`; browser login flow works
end to end over HTTPS with the refresh cookie visibly `Secure` in dev tools
and actually sent back on subsequent requests (the thing plain-HTTP `curl`
testing in Stage 1 couldn't prove). No local/CI verification applies — there
is no domain, host Caddy instance, or TLS cert available in dev to test
against.

---

## Stage 3 — Data Protection key persistence

**Status: Done**

- Today, nothing calls `AddDataProtection()` explicitly, so ASP.NET Core falls
  back to its default key ring behavior — in a container this is not
  guaranteed to survive a redeploy/recreate. The **refresh token** itself is
  unaffected (it's a hand-rolled, hashed, DB-stored token per ADR 0009 — not
  Data-Protection-encrypted), but the **access token**
  (`BearerTokenOptions.BearerTokenProtector`, per `api/CLAUDE.md`'s hard
  rules) is minted via the Data Protection key ring: losing it on every
  deploy silently invalidates every currently-issued access token
  mid-session, forcing an immediate interceptor-driven refresh for anyone
  using the app at deploy time. Low-severity for a single-user app, but a
  deploy-time surprise worth removing deliberately rather than leaving as an
  accident of the default.
- Add `AddDataProtection().PersistKeysToFileSystem(...)` pointed at a
  named Docker volume (mirrors the existing `postgres-data` volume pattern in
  `docker-compose.yml`), with `SetApplicationName(...)` pinned to a stable
  string so the key ring's purpose strings don't drift if the container
  hostname changes between deploys.

**Verify:** log in, note the access token, `docker compose restart api`
(not full recreate — but test the volume survives a full `down`/`up` too),
confirm the pre-restart access token is still accepted until its natural
15-minute expiry rather than immediately rejected.

---

## Stage 4 — Production secrets & closing the Postgres exposure

**Status: Done**

- Set real `SEED_USER_NAME`/`SEED_USER_PASSWORD` via the server's `.env`
  (never committed — `.env` is already gitignored per the existing Compose
  setup) and rotate `POSTGRES_PASSWORD` off the `streaks_dev` placeholder
  that ships in `.env.example`.
- **Found while planning this phase, not yet fixed:** `docker-compose.yml`
  maps Postgres as `"5433:5432"`, which binds to all interfaces by default —
  on a real Hetzner box with a public IP, that's Postgres reachable directly
  from the internet on port 5433 unless something else blocks it. Only the
  `api` container needs to reach `postgres`, and they're already on the same
  Compose network — change the mapping to `"127.0.0.1:5433:5432"` (keeps the
  convenient host-side access for one-off debugging/psql) or drop the port
  mapping entirely and require `docker compose exec postgres psql ...` for
  that. Decide which before implementing.
- Manually verify decision #12 (`docs/phase-3-plan.md`) actually fires
  correctly against a genuinely fresh Production boot: empty `Users` table,
  `ASPNETCORE_ENVIRONMENT=Production`, no `SEED_USER_PASSWORD` set — should
  refuse to boot, not silently fall back to the dev password. This has only
  been unit-tested against `DatabaseInitializer.ResolveSeedCredentials` in
  isolation (Stage 3, Phase 3) — worth one real `docker compose up` against
  an empty volume to see the actual startup failure.

**Verify:** `docker compose config` (resolves env interpolation) reviewed by
eye for anything still defaulted; `nmap -p 5433 <server-ip>` (or `curl`
attempting a raw TCP connect) from outside the Hetzner box confirms Postgres
is unreachable; the empty-`Users`-table + no-`SEED_USER_PASSWORD` boot-failure
check above passes.

---

## Stage 5 — Server provisioning & hardening

**Status: Done (human).** All 13 items below are closed.

Server-side work (SSH access, OS config, firewall rules) the human is doing
directly rather than delegating. Decided in a dedicated security-planning
discussion before any server work started — recorded here (with the "why")
so it doesn't need re-litigating, same as any other decision in this repo's
plans:

1. **Done (human).** **SSH: key-only, no password auth.** The private key
   lives on the human's own PC, passphrase-protected — a bare unencrypted
   key would mean anyone with read access to that machine (malware, theft,
   a stray sync/backup) gets immediate server access with no second factor;
   the passphrase closes that gap. `PasswordAuthentication no` in
   `sshd_config`.
2. **Done (human).** **Dedicated non-root deploy user**, not root, for both
   CD and routine admin access — bounds the blast radius if the deploy
   credential (#5) or the human's own session is ever compromised.
3. **Done (Stage 4).** **Postgres port fix:** bind `127.0.0.1:5433:5432` in
   `infrastructure/docker-compose.yml` — keeps host-side `psql` access for
   one-off debugging while making the bind itself (not just a firewall
   rule) the thing that blocks external reachability, i.e. still safe even
   if the firewall layer below has a gap. Landed as part of Stage 4, not a
   separate action.
4. **Done (human).** **Firewall: layered.** Hetzner Cloud Firewall (outside
   the box) *and* `ufw` (inside the box), both allowing only 22/80/443 —
   defense in depth, so a misconfiguration in one layer doesn't fully
   expose the box on its own.
5. **Done (human).** **Deploy secret: a scoped, deploy-only SSH key**, not
   the human's personal key — generated specifically for CI, authorized
   only for the deploy user's limited actions (#2). If the GitHub Actions
   secret ever leaks, the damage is bounded to "can deploy," not "has the
   human's own account."
6. **Done.** **Docker image versions pinned to exact tags** — checked what
   was actually running (not assumed) so pinning didn't silently become an
   upgrade: `postgres:18-alpine` → `postgres:18.4-alpine`, `caddy:2-alpine`
   → `caddy:2.11.4-alpine` (`web/Dockerfile`),
   `mcr.microsoft.com/dotnet/aspnet:10.0` → `...:10.0.10` (`api/Dockerfile`).
   `dotnet/sdk:10.0.302` and `node:24.18.0-alpine` were already exact-pinned
   from earlier stages.
7. **Done (human).** **`fail2ban` (or equivalent) on SSH** — standard
   mitigation against credential-guessing once the box has a public IP
   that will get scanned, independent of #1 already being key-only.
8. **Done (human).** **Automatic OS security updates enabled from day one**
   (e.g. `unattended-upgrades` on Debian/Ubuntu) — not deferred as a "get
   to it later."
9. **Done.** **Rate limiter in front of `POST /api/auth/login`**, on top of
   Identity's existing lockout policy (Phase 3 decision #10) — lockout
   alone only protects one *known* account from repeated guesses; a rate
   limiter mitigates broader credential-stuffing traffic against the
   endpoint. Implemented as a **global** (not per-IP) fixed-window limiter
   (`Api/Infrastructure/RateLimitingServiceCollectionExtensions.cs`,
   10 requests/minute) — the request path has two proxy hops (host Caddy →
   compose Caddy → `api`) and nothing trusts `X-Forwarded-For` yet, so
   per-IP partitioning would need `ForwardedHeadersMiddleware` + trusted-
   proxy config to avoid spoofing; a global cap was judged sufficient for a
   single-account app. ASP.NET Core's built-in rate-limiting middleware —
   no new dependency, no ADR needed. **Enforced only outside Development**
   (`environment.IsDevelopment()` gate, same pattern as the `Secure` cookie
   and Data Protection persistence) — found during verification that
   `IntegrationTestBase.InitializeAsync` logs in through this exact endpoint
   for every single test, which blew straight through a real 10/min cap and
   failed 7 tests; Development still runs unlimited, Production/Docker
   Compose (where `ASPNETCORE_ENVIRONMENT` is always `Production`) still
   gets the real limit.
10. **Done — 4 of 5.** **Security headers** — HSTS,
    `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options` added to
    `infrastructure/Caddyfile`. **CSP deferred**, tracked in
    `docs/existing-problems.md`: the built `index.html` has an inline
    `<style>`/`onload` from Angular's automatic critical-CSS inlining, so a
    compatible CSP would need `'unsafe-inline'` on `script-src`, defeating
    most of its point — doing it properly needs an `web/angular.json`
    build-config change first.
11. **Done (human).** **Seeded username chosen to not be guessable** — not
    `dev`/`admin`/the obvious default now that this is a real Production
    credential (the account itself already isn't enumerable, per Phase 3's
    generic 401 message, but an unguessable username removes the easier
    first guess entirely). `SEED_USER_NAME` set to its real value in the
    server `.env`.
12. **Done (human).** **`.env` on the server: minimal file permissions**
    (owner-read-only, owned by the deploy user — not group/world-readable) —
    holds `SEED_USER_PASSWORD`/`POSTGRES_PASSWORD` in plaintext, so
    filesystem permissions are the only thing standing between "any account
    on the box" and full credential access.
13. **Done (Stage 6).** **Dependency/image scanning** — `dotnet list
    package --vulnerable` runs as an informational CI step alongside the
    existing `npm audit` tracking (`docs/existing-problems.md`), and GitHub
    Dependabot alerts are enabled for npm + NuGet. Image scanning (e.g.
    Trivy) was not added — judged unnecessary on top of exact-pinned base
    images (#6) and Dependabot already covering the dependency surface.

**Verify:** `nmap`/external `curl` against the Hetzner IP shows only
22/80/443 reachable; SSH password auth attempt is rejected; `ssh` as the
deploy user cannot `sudo` beyond what's needed; `ls -la .env` on the server
shows owner-only permissions; a scripted burst of failed `/api/auth/login`
attempts gets rate-limited before Identity's own lockout would trigger;
`docker compose config` shows no floating tags.

---

## Stage 6 — GitHub Actions: CI gate + CD on push to main

**Status: Done.**

ADR 0010 (`docs/decisions/0010-deploy-mechanism.md`) records the deploy
mechanism decision: CI builds `api`/`caddy` images and pushes them to GHCR
(public, no server-side pull credential needed); `.github/workflows/deploy.yml`
triggers via `workflow_run` on CI going green on `main` (not a parallel
`push` trigger, so it can't race ahead of CI), then SSHes in and runs
`docker compose -f docker-compose.yml -f docker-compose.prod.yml pull && up
-d`. Deploy is fully automatic, rollback is manual for now (SSH in, pull a
previous `:<commit-sha>` tag) — see the ADR's Rationale for why each of
these was chosen over the alternative.

- `.github/workflows/ci.yml` — `api` and `web` jobs in parallel, every PR +
  push to `main`: `dotnet test` (Testcontainers, ADR 0003), `ng lint`,
  `ng test` (Angular's Vitest-based `@angular/build:unit-test`, not Karma —
  confirmed by reading `angular.json`), `ng build`. Also an informational
  `dotnet list package --vulnerable` step (Stage 5 decision #13).
- `.github/workflows/deploy.yml` — builds+pushes both images, then the SSH
  deploy step. Third-party actions SHA-pinned, GitHub-owned ones tag-pinned
  (ADR 0010).
- `infrastructure/docker-compose.prod.yml` (new) — server-only override
  pointing `api`/`caddy` at the pushed GHCR images; local dev's
  `docker compose up --build` is untouched (base file unchanged).
- `infrastructure/deploy.sh` (new) — the actual pull + `up -d` script,
  version-controlled here. `deploy.yml`'s SSH step just invokes
  `/home/deploy/deploy.sh` — the `deploy` SSH user's key is restricted to a
  forced command in `authorized_keys` (`command="/home/deploy/deploy.sh"`),
  so this is the only thing that key can ever run regardless of what's
  sent. The app lives at `/opt/streaks` on the server (FHS convention for
  self-installed software) — `deploy.sh` assumes that path.

**Human-owned setup — done:** `DEPLOY_HOST`/`DEPLOY_USER` (`deploy`)/
`DEPLOY_SSH_KEY` repo secrets added; branch protection on `main` configured
requiring the `api`/`web` CI checks; the two GHCR packages confirmed public
after the first push; Dependabot alerts enabled for npm + NuGet.

**Also needed — and note this is *not* a git clone.** `docker-compose.yml`'s
only bind-mount from disk is `./Caddyfile` — everything else is either a
named Docker volume (Docker-managed, not a repo file) or a `build:` context
that `deploy.sh` never touches (it only runs `pull` + `up -d`, never
`--build`). So the server needs exactly **four files** under
`/opt/streaks/infrastructure/`, not the repo:
`docker-compose.yml`, `docker-compose.prod.yml`, `Caddyfile`, and `.env`
(copied from `.env.example` with real `POSTGRES_PASSWORD`/`SEED_USER_NAME`/
`SEED_USER_PASSWORD` filled in). Copy those four there (`scp` from a local
checkout, or paste manually — they rarely change); copy
`infrastructure/deploy.sh` to `/home/deploy/deploy.sh` and `chmod +x` it;
add the `command="/home/deploy/deploy.sh"` restriction to the deploy key's
line in `~deploy/.ssh/authorized_keys`.

**Known limitation worth knowing about, not an oversight:** CD doesn't sync
config files, only Docker images. If `Caddyfile` or either compose file
ever changes in the repo, the server's copies don't update automatically —
`deploy.yml` never pushes them. Re-copy by hand after such a change (same
for `deploy.sh` itself if it's ever edited).

**Host-level Caddy** (outside this repo, per the Stage 2 decision — TLS
termination and the real domain live there, not in `infrastructure/Caddyfile`):

```
your-domain.example {
	reverse_proxy localhost:8080
}
```

That's the whole thing — Caddy's automatic HTTPS kicks in on its own once a
real domain is the site address instead of a bare `:port`. Security headers
are already set by the compose Caddy and pass through `reverse_proxy`
untouched, so no need to duplicate them here.

**Verify:** confirmed — a push to `main` went through CI and was live on the
domain within the pipeline's run time. Branch protection requiring the
`api`/`web` checks (set up as part of the human-owned setup above) is what
now enforces that a failing check on a PR blocks the merge and never reaches
the deploy job.

---

## Stage 7 — Two-week dogfooding + close-out

**Status: Done.**

- Not a code stage — the brief's exit criterion ("use it daily for two
  weeks") is calendar-based, not a diff to review. Rather than gate closing
  the phase on waiting out a fixed two-week window, the phase is being
  closed now; day-to-day dogfooding continues informally alongside whatever
  comes next.
- Any bugs, friction, or missing features that turn out to matter will be
  logged to `docs/existing-problems.md` or `docs/troubleshooting.md` as they
  come up, same as Phases 2–3's pattern, rather than as a one-time
  retrospective batch at a fixed two-week mark.

**Phase 4 (Ship to Hetzner) is complete.**

---

## Critical files for this phase

- `infrastructure/docker-compose.yml`, `infrastructure/Caddyfile`,
  `infrastructure/.env.example` — topology, TLS, port exposure (Stages 1, 2, 4).
- `web/Dockerfile` (new) — Angular build stage feeding Caddy (Stage 1).
- `api/Api/Program.cs` — `AddDataProtection()` wiring (Stage 3);
  `AuthEndpoints.SetRefreshCookie`'s `Secure` gate re-check (Stage 2).
- `.github/workflows/` (new) — CI + CD pipelines (Stage 6).
- `docs/decisions/0010-*.md` (new, name TBD) — deploy mechanism ADR (Stage 6).

## Process notes for execution

- One scoped stage per turn, per project convention — do not batch multiple
  stages into a single implementation turn.
- Stage 5 is explicitly excluded from that loop: it's server-side work the
  human owns directly, not something to delegate to a Claude Code turn.
