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

## Stage 2 — Domain + TLS (Caddy automatic HTTPS)

**Status: Not started**

- Requires a real domain with a DNS `A`/`AAAA` record pointing at the Hetzner
  box's public IP — **server/DNS-side task, not code** (see Stage 5).
- Replace the Caddyfile's `:80` catch-all with the real domain name so Caddy's
  automatic HTTPS (Let's Encrypt via ACME) issues and renews a certificate
  without extra config; Caddy redirects HTTP → HTTPS by default once a domain
  is configured.
- Flip `Secure` on the refresh cookie to unconditionally true in this
  topology if it isn't already purely environment-gated end to end (re-check
  `AuthEndpoints.SetRefreshCookie`'s `!env.IsDevelopment()` still does the
  right thing once "Production" means "real HTTPS domain" instead of
  "Docker Compose over plain HTTP" — worth a manual `curl -v` confirmation
  either way, not just a code read).

**Verify:** `curl -v https://<domain>/api/health` shows a valid cert chain,
`curl -v http://<domain>/` redirects to `https://`, browser login flow works
end to end over HTTPS with the refresh cookie visibly `Secure` in dev tools.

---

## Stage 3 — Data Protection key persistence

**Status: Not started**

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

**Status: Not started**

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

**Status: Owned by the human — not a Claude Code implementation stage.**

Server-side work (SSH access, OS config, firewall rules) the human is doing
directly rather than delegating. Decided in a dedicated security-planning
discussion before any server work started — recorded here (with the "why")
so it doesn't need re-litigating, same as any other decision in this repo's
plans:

1. **SSH: key-only, no password auth.** The private key lives on the human's
   own PC, passphrase-protected — a bare unencrypted key would mean anyone
   with read access to that machine (malware, theft, a stray sync/backup)
   gets immediate server access with no second factor; the passphrase closes
   that gap. `PasswordAuthentication no` in `sshd_config`.
2. **Dedicated non-root deploy user**, not root, for both CD and routine
   admin access — bounds the blast radius if the deploy credential (#5) or
   the human's own session is ever compromised.
3. **Postgres port fix:** bind `127.0.0.1:5433:5432` in
   `infrastructure/docker-compose.yml` (Stage 4) rather than dropping the
   mapping — keeps host-side `psql` access for one-off debugging while
   making the bind itself (not just a firewall rule) the thing that blocks
   external reachability, i.e. still safe even if the firewall layer below
   has a gap.
4. **Firewall: layered.** Hetzner Cloud Firewall (outside the box) *and*
   `ufw` (inside the box), both allowing only 22/80/443 — defense in depth,
   so a misconfiguration in one layer doesn't fully expose the box on its
   own.
5. **Deploy secret: a scoped, deploy-only SSH key**, not the human's personal
   key — generated specifically for CI, authorized only for the deploy
   user's limited actions (#2). If the GitHub Actions secret ever leaks, the
   damage is bounded to "can deploy," not "has the human's own account."
6. **Docker image versions pinned to exact tags** (e.g. `postgres:18.1-alpine`,
   not `postgres:18-alpine`) in `infrastructure/docker-compose.yml` — a
   redeploy never silently pulls a newer, untested image.
7. **`fail2ban` (or equivalent) on SSH** — standard mitigation against
   credential-guessing once the box has a public IP that will get scanned,
   independent of #1 already being key-only.
8. **Automatic OS security updates enabled from day one** (e.g.
   `unattended-upgrades` on Debian/Ubuntu) — not deferred as a "get to it
   later."
9. **Rate limiter in front of `POST /api/auth/login`**, on top of Identity's
   existing lockout policy (Phase 3 decision #10) — lockout alone only
   protects one *known* account from repeated guesses; a rate limiter
   mitigates broader credential-stuffing traffic against the endpoint.
   ASP.NET Core's built-in rate-limiting middleware — no new dependency, no
   ADR needed.
10. **Security headers added now, not deferred** — HSTS, CSP,
    `X-Content-Type-Options`, `Referrer-Policy`, frame-ancestors, a few lines
    in the Caddyfile while it's already being touched for Stages 1–2.
11. **Seeded username chosen to not be guessable** — not `dev`/`admin`/the
    obvious default once this is a real Production credential (the account
    itself already isn't enumerable, per Phase 3's generic 401 message, but
    an unguessable username removes the easier first guess entirely).
12. **`.env` on the server: minimal file permissions** (owner-read-only,
    owned by the deploy user — not group/world-readable) — holds
    `SEED_USER_PASSWORD`/`POSTGRES_PASSWORD` in plaintext, so filesystem
    permissions are the only thing standing between "any account on the box"
    and full credential access.
13. **Dependency/image scanning** — `dotnet list package --vulnerable`
    alongside the existing `npm audit` tracking
    (`docs/existing-problems.md`), GitHub Dependabot alerts, optionally
    image scanning (e.g. Trivy) in CI. Not gone through the same one-by-one
    discussion as #1–12 above; revisit before Stage 6 if it needs its own
    pass.

**Verify:** `nmap`/external `curl` against the Hetzner IP shows only
22/80/443 reachable; SSH password auth attempt is rejected; `ssh` as the
deploy user cannot `sudo` beyond what's needed; `ls -la .env` on the server
shows owner-only permissions; a scripted burst of failed `/api/auth/login`
attempts gets rate-limited before Identity's own lockout would trigger;
`docker compose config` shows no floating tags.

---

## Stage 6 — GitHub Actions: CI gate + CD on push to main

**Status: Not started**

- **ADR needed first** (project convention: architecture decision before
  code, same rigor as ADR 0008 before Phase 3's auth work) — decide the
  deploy mechanism: CI builds and pushes images to a registry (e.g. GHCR)
  and the server pulls, vs. the server builds directly from a `git pull` +
  `docker compose up --build` triggered over SSH. Depends on what Stage 5
  decides about the deploy credential's shape and privilege.
- **CI (every PR + push):** `dotnet test`, `ng test`, `ng lint`, `ng build`.
  Branch protection on `main` requiring these before merge.
- **CD (push to main, after CI is green):** deploy to the Hetzner box per
  the ADR's chosen mechanism. Uses a deploy credential scoped as narrowly as
  Stage 5 decided (a GitHub Actions secret, not a broadly-privileged key).

**Verify:** a trivial change pushed to `main` is live on the domain within
the pipeline's run time; a deliberately failing test on a PR blocks the merge
and never reaches the deploy job.

---

## Stage 7 — Two-week dogfooding + close-out

**Status: Not started**

- Not a code stage — the exit criterion is calendar-based per the brief
  ("use it daily for two weeks"), not a diff to review.
- Log anything found during real use — bugs, friction, a missing feature
  that turns out to matter — as follow-up items; add genuinely durable
  gotchas to `docs/existing-problems.md` or `docs/troubleshooting.md` as they
  come up, same as Phases 2–3 did.

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
