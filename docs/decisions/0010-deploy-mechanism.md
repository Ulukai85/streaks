# ADR 0010 — Deploy mechanism: GHCR build-and-push in CI, SSH pull on the server

Date: 2026-08-01
Status: Accepted

## Context

Phase 4 (`PROJECT-BRIEF.md` §11) requires "GitHub Actions on push to main."
Stage 6 of `docs/phase-4-plan.md` calls out the central question this ADR
answers: how does a push to `main` actually get onto the Hetzner box? The
realistic options are building the deployable image once in CI and having
the server pull it from a registry, or having the server itself run
`git pull` + `docker compose up --build` over SSH. Stage 5 already settled
that CD uses a scoped, deploy-only SSH key (`docs/phase-4-plan.md` decision
#5) — that constrains the transport (SSH) but not which of the two above
approaches runs on the other end of it.

## Decision

- **CI builds `api`/`caddy` images once and pushes them to GHCR
  (`ghcr.io`)**, tagged `:latest` and `:<commit-sha>`. The server never
  builds anything — it only pulls.
- **CD triggers via `workflow_run`** on the CI workflow completing
  successfully on `main` (`.github/workflows/deploy.yml`), not on a
  parallel `push` trigger — so a direct push to `main` that somehow
  bypasses a PR still can't reach the deploy job without CI having actually
  gone green first.
- **Deploy is fully automatic** on a successful CD run — no manual approval
  gate.
- **The deploy SSH key is further restricted to a forced command.**
  `~deploy/.ssh/authorized_keys` on the server pins the key to
  `command="/home/deploy/deploy.sh"`, so the key can never run arbitrary
  commands regardless of what a workflow (or anyone holding the key) sends
  — only that one script, ever. `infrastructure/deploy.sh` is the
  version-controlled source of truth for it; the server-side copy at
  `/home/deploy/deploy.sh` is what's actually installed and must be kept in
  sync by hand (it's a human-owned server file, same category as Stage 5's
  `sshd_config`/`ufw` rules — not something CI pushes). The app itself lives
  at `/opt/streaks` on the server, following the FHS convention for
  self-installed application software (as opposed to `/usr`, which is
  OS-managed) — but this is **not a git clone**. Since `docker-compose.yml`'s
  only bind-mount from disk is `./Caddyfile`, and `deploy.sh` never runs
  `--build`, the server only needs four files under
  `/opt/streaks/infrastructure/`: `docker-compose.yml`,
  `docker-compose.prod.yml`, `Caddyfile`, `.env`. No `api/`, no `web/`, no
  `.git`.
- **GHCR packages are public.** No pull credential is needed on the server
  at all; `docker compose pull` just works. The images contain only
  compiled app code (the API binary, the Angular build, Caddy config) —
  actual secrets (`POSTGRES_PASSWORD`, `SEED_USER_PASSWORD`) come from the
  server's `.env` at runtime (Stage 4) and are never baked into an image.
- **Rollback is manual for now**: SSH in, `docker compose pull` a previous
  `:<commit-sha>` tag (or re-point the compose override and re-deploy).
  No one-click rollback workflow.
- **Third-party GitHub Actions are pinned to a full commit SHA** (with a
  version comment), not a mutable tag — `docker/setup-buildx-action`,
  `docker/login-action`, `docker/build-push-action`, `appleboy/ssh-action`.
  GitHub-owned actions (`actions/checkout`, `actions/setup-dotnet`,
  `actions/setup-node`) stay tag-pinned (`@vN`) — GitHub's own release
  process is the trust boundary already relied on for the runner itself.
- `infrastructure/docker-compose.yml` (the base file, used for local dev)
  keeps `build:` for `api`/`caddy` unchanged. A new
  `infrastructure/docker-compose.prod.yml` overrides only `image:` for
  those two services; the server runs
  `docker compose -f docker-compose.yml -f docker-compose.prod.yml pull &&
  ... up -d`. Local `docker compose up --build` is untouched by this ADR.

## Rationale

- **Build-once-in-CI over rebuild-on-server** because the image that
  passed `dotnet test`/`ng test`/`ng lint` in CI is byte-for-byte what runs
  in production — there is no window where a rebuild on the server behaves
  differently from what CI verified. Rebuilding on the server would also
  spend the live box's CPU/RAM on every deploy, alongside the containers
  already serving traffic.
- **`workflow_run` over a parallel `push` trigger** because "CD on push to
  main, after CI is green" (the phase plan's own phrasing) is a real
  ordering dependency, not just a coincidence of both workflows reacting to
  the same event. A parallel `push` trigger on the deploy workflow would
  race the CI workflow rather than wait for it.
- **Fully automatic deploy, no approval gate** because this is a
  single-developer personal app — the human is already the sole approver
  of every merge to `main`; a second manual click before deploy adds
  friction without a distinct reviewer to catch anything.
- **Public GHCR packages** to avoid a second server-side credential to
  provision and rotate, on top of the deploy SSH key. Rejected keeping them
  private: it would need a `read:packages`-scoped PAT logged into
  `docker` on the server, which is one more secret whose compromise matters
  (`docker login` credentials cached on disk), for images that contain no
  secrets in the first place.
- **Manual rollback over a rollback workflow**, matching this phase's
  existing pattern of deliberately deferring things rather than
  half-building them (see backups → Phase 5): a single-user personal app
  rarely needs push-button rollback, and the `:<commit-sha>` tags already
  give a human everything needed to do it by hand when it does come up.
- **SHA-pin third-party actions, tag-pin GitHub's own** because a
  compromised or force-moved tag on a third-party action is a real supply-
  chain vector (an attacker who gains control of, say, `appleboy/ssh-action`
  could silently re-point `v1` at malicious code); GitHub's own actions
  carry materially lower risk given they're already the trust boundary for
  the runner environment itself, so hash-pinning them buys less for the
  same maintenance cost (illegible diffs, needing to manually track new
  SHAs on every bump).

## Consequences

- A new commit on `main` takes two workflow runs (CI, then CD) to reach
  production, not one — slightly slower than a single combined workflow,
  in exchange for `deploy.yml` only ever running against code that's
  already been proven green.
- The server needs Docker's registry-pull path working (`docker compose
  pull`) in addition to what it already does — no new install, `docker
  compose` already handles this.
- Third-party action version bumps require manually re-resolving a new
  commit SHA (e.g. via the GitHub API or `git ls-remote`) instead of just
  editing a version number — accepted as the cost of the stronger
  supply-chain posture. Dependabot can be configured to open these bump PRs
  automatically (it understands SHA-pinned actions and proposes the new
  SHA + updates the version comment) if that maintenance becomes
  noticeable.
- If GHCR public visibility is ever reconsidered (e.g. the images start
  containing something more sensitive than compiled app code), this ADR's
  "no server credential" consequence goes away and a PAT-based login step
  would need adding back on the server — flagged here so it isn't a
  surprise if that assumption changes.
- **CD doesn't sync config files, only Docker images.** `deploy.yml` builds
  and pushes images, then `deploy.sh` pulls and restarts — it never copies
  `Caddyfile` or the compose files to the server. If either changes in the
  repo, the server's copies go stale until manually re-copied. This is a
  direct consequence of the server holding only four files rather than a
  git clone (see Decision above) — there's no `git pull` step to make it
  self-updating. Acceptable for now since these files change rarely; worth
  revisiting (e.g. having `deploy.sh` fetch them, or CD `scp` them) if that
  stops being true.
