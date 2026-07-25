# Phase 2 — Domain (challenges, completions, streaks) + minimal UI

> Working plan for Phase 2 (see `PROJECT-BRIEF.md` §11). Tracks stage status
> as work lands; not a source-of-truth domain doc — see `docs/domain.md` for
> that.

## Context

Phase 1 delivered a walking skeleton: a bare .NET 10 Minimal API (`Program.cs`,
empty `AppDbContext`, one `Features/Health/` vertical slice) and a matching
Angular 22 page (`HealthStatus`, using `httpResource`, spartan-ng Helm
`button`/`card` already generated). No auth, no domain entities, no
deployment yet.

Phase 2 is where the actual product gets built: per PROJECT-BRIEF.md §1, v1's
full scope is "add a challenge, see today's dashboard, tick it off, see the
streak" — and since Phase 3 is auth-only and Phase 4 is deployment-only, this
phase has to deliver an end-to-end usable app (without auth) on its own.

**Confirmed decisions (do not re-litigate):**
- New minimal `User` table now: `Id (Guid), TimeZoneId`. No credentials/identity
  fields — Phase 3 extends this same table with ASP.NET Core Identity fields
  rather than replacing it. One hardcoded dev user seeded via migration,
  `TimeZoneId = "Europe/Berlin"`.
- Phase 2 includes UI: backend API + enough Angular UI to actually use the app
  daily (add challenge, dashboard, tick off, streak display).
- Primary keys: `Guid` across `User`/`Challenge`/`Completion`.
- `Challenge.Color`: fixed palette (~6-8 swatches) via spartan `toggle-group`,
  not a free hex input.
- The retroactive-bound check (current period + previous two, else 400) is
  agent-written — it's fully specified in brief §6.3 and only steps back N
  periods from an already-resolved `PeriodStart` (no timezone/DST logic).

**Off-limits (human-owned, per api/CLAUDE.md and PROJECT-BRIEF.md §10):**
`PeriodStartFor` (signature: `static DateOnly PeriodStartFor(DateTimeOffset
instant, string timeZoneId, Cadence cadence)`, §7) and the streak
gaps-and-islands SQL (§8), **including their test suites**. The agent must
never write these or invent domain rules for them — stop and hand off at the
checkpoints below. The NodaTime-vs-`TimeZoneInfo` ADR (§7) is the human's call
too, decided alongside the implementation.

Everything else — CRUD endpoints, DTOs, validators, EF config, migrations,
Angular components/services/routing, test scaffolding (not the period/streak
assertions) — is agent-owned per §10.

---

## Stage 0 — Cross-cutting backend setup

**Status: Done**

- `api/Api/Program.cs`: add `AddProblemDetails()` + exception/status-code
  handling so RFC 7807 responses are automatic; add a small reusable
  `IEndpointFilter` (`Api/Infrastructure/ValidationFilter.cs`) that resolves
  `IValidator<T>` per endpoint and returns a validation-problem result on
  failure (ADR 0004: validators via endpoint filters, not
  `FluentValidation.AspNetCore`). Validators are registered manually per
  feature as they're built (Stage 4/5), not via assembly scanning — avoids
  pulling in `FluentValidation.DependencyInjectionExtensions` before any
  validator exists to scan for. Add `public partial class Program;` at the
  bottom so `WebApplicationFactory<Program>` works in Stage 2.
- Add `Microsoft.EntityFrameworkCore.Design` to `Directory.Packages.props` +
  `Api.csproj` (needed for `dotnet ef migrations add`) — noted as an ADR 0004
  addendum rather than a new ADR file (tooling package for an already-decided
  dependency, same reasoning ADR 0004 itself gives for Testcontainers/ADR 0003).

**Verify:** `dotnet build` clean under `TreatWarningsAsErrors`; `/api/health`
still works; a unit test on `ValidationFilter<T>` covering both an invalid
request (returns a 400 validation-problem result, next() not called) and a
valid request (calls next()).

---

## Stage 1 — Domain entities, EF config, migrations, seed user

**Status: Not started**

Use the existing (currently empty) `Api/Domain/` folder for entities and enums.

1. `Cadence` enum (`Api/Domain/Cadence.cs`): `Daily, Weekly, Monthly`, mapped
   via `HasConversion<string>()` → persisted as `"Daily"/"Weekly"/"Monthly"`.
   Must exist before Checkpoint A since the human's `PeriodStartFor` signature
   depends on it.
2. `User` entity (`Api/Domain/User.cs`: `Id, TimeZoneId`) + EF config
   (`Api/Data/Configurations/UserConfiguration.cs`) with `HasData(...)` seeding
   the dev user (`Europe/Berlin`). Add `DbSet<User> Users` to `AppDbContext`,
   wire `ApplyConfigurationsFromAssembly` in `OnModelCreating`.
3. `Challenge` entity + config: FK to `User`, `Cadence` as string,
   index on `(UserId, ArchivedAt)`.
4. `Completion` entity + config: FK to `Challenge`, **unique index on
   `(ChallengeId, PeriodStart)`** (DB-enforced per ADR 0003).
5. Generate migrations per entity (`dotnet ef migrations add ...`).

**Verify:** clean migrations, `dotnet build`; apply against local Postgres
(or rely on Stage 2's automated `MigrateAsync`) to confirm they run and the
seed row inserts.

---

## Stage 2 — Integration test harness (blocks all later backend tests)

**Status: Not started**

Only a raw-Npgsql smoke test exists today — nothing runs EF migrations or
exercises `AppDbContext`/endpoints yet.

- `Api.Tests/Infrastructure/PostgresFixture.cs`: `IAsyncLifetime` wrapping
  `PostgreSqlBuilder("postgres:18-alpine")`, shared via
  `ICollectionFixture<PostgresFixture>`; on init, runs
  `AppDbContext.Database.MigrateAsync()` against the container. Include a
  `ResetDatabaseAsync()` helper (`TRUNCATE` challenges/completions, leave
  `Users` alone) for per-test isolation.
- `Api.Tests/Infrastructure/ApiFactory.cs`: `WebApplicationFactory<Program>`
  pointed at the fixture's connection string, so HTTP-level tests exercise the
  real pipeline (validation filter, problem details) against real Postgres.
  Requires adding `Microsoft.AspNetCore.Mvc.Testing` (flag as new-NuGet).
- `Api.Tests/Infrastructure/IntegrationTestBase.cs`: combines both for
  feature test classes.

**Verify:** a trivial test asserting the seeded dev user round-trips through
`AppDbContext` with `AsNoTracking()`; `dotnet test` green.

---

## Stage 3 — HUMAN CHECKPOINT A: `PeriodStartFor`

**Status: Not started**

**Hard stop.** Agent does not write the function, file, or tests. Confirm:
- Signature per §7, using the `Cadence` enum from Stage 1.
- Location: `Api/Domain/PeriodCalculator.cs`, static class `PeriodCalculator`.
- NodaTime vs `TimeZoneInfo`/IANA — human's ADR + implementation.

**Blocked on this:** Completion tick-off (Stage 5.2), Dashboard endpoint
(Stage 7). **Not blocked:** Challenge CRUD (Stage 4) and its UI (Stage 8) —
zero dependency on period math, proceed in parallel.

---

## Stage 4 — Challenge CRUD endpoints (parallel to Stage 3)

**Status: Not started**

Vertical slice under `Api/Features/Challenges/`, pattern-matched against
`Features/Health/`.

1. DTOs + validator: `CreateChallengeRequest`, `ChallengeResponse`,
   `CreateChallengeRequestValidator` (name required, cadence must parse,
   valid URL if present, color in the fixed palette, non-negative sort
   order). `TargetCount` is not in the request DTO — server always persists
   `1`.
2. `POST /api/challenges` — create, hardcoded dev-user id, 201 +
   `ChallengeResponse`.
3. `GET /api/challenges` — `AsNoTracking()`, non-archived by default, ordered
   by `SortOrder`.
4. `POST /api/challenges/{id}/archive` — sets `ArchivedAt`, 404 if missing.

No general update endpoint — brief's v1 scope only lists create + tick-off;
archive is the only other listed mutation.

**Verify:** `dotnet test --filter Challenges` after each endpoint — create
persists/validates, list shape/ordering, archive sets `ArchivedAt` and is
idempotent-safe.

---

## Stage 5 — Completion tick-off endpoint

**Status: Not started**

1. DTOs + validator (not blocked, can start alongside Stage 4):
   `CompleteChallengeRequest` (`DateOnly? PeriodStart, string? Note` — omitted
   means current period), `CompletionResponse`,
   `CompleteChallengeRequestValidator` (shape only, e.g. note max length).
2. `POST /api/challenges/{challengeId}/completions` (**hard-blocked on
   Checkpoint A**): resolve current period via `PeriodCalculator`; if a
   `PeriodStart` is given, validate it's within {current, current-1,
   current-2} using an agent-written stepping-back helper (daily →
   `AddDays`, weekly → `AddDays(-7/-14)` since `PeriodStart` is already the
   Monday, monthly → `AddMonths(-1/-2)` since it's already the 1st) — else
   400. Rely on the Stage 1 unique index to reject duplicate tick-offs
   (Postgres unique-violation → mapped problem-details response).

**Verify:** `dotnet test --filter Completions` — happy path, retroactive
within/beyond bound, duplicate completion conflict (must exercise the real
Postgres unique index per ADR 0003).

---

## Stage 6 — HUMAN CHECKPOINT B: streak SQL (parallel to Stages 3-5)

**Status: Not started**

**Hard stop.** Only needs the `Completion` table (Stage 1), so it can happen
any time after Stage 1. Confirm:
- Location: `Api/Features/Streaks/StreakQuery.cs` (or colocated with
  Dashboard), using `db.Database.SqlQuery<T>()` per ADR 0002.
- Result contract dependents will code against (e.g. a
  `StreakResult(int Length, bool IsAlive)`-shaped record) — ask the human,
  don't guess.
- Covers: current-or-previous-ordinal "alive" rule, periods before
  `StartsOn` excluded, archived challenges freeze their streak.

**Blocked on this:** streak display on the Dashboard (Stage 7.2) and Angular
streak badge (Stage 9.3). Not blocked: everything else in Stage 7.

---

## Stage 7 — Dashboard read endpoint

**Status: Not started**

1. **Open/due + done-this-period grouping** (blocked on Checkpoint A only):
   `GET /api/dashboard` — `Api/Features/Dashboard/DashboardEndpoints.cs` +
   `DashboardResponse` (`Open: [...]`, `DoneThisPeriod: [...]`). Loads dev
   user's non-archived, started challenges (`AsNoTracking()`), computes each
   one's current `PeriodStart`, checks for a matching `Completion` to sort
   open vs. done, computes days-remaining-in-period for urgency sort within
   the open group (§6.4). Streak field stubbed as a placeholder (e.g. `null`)
   until step 2 — this is the one deliberate placeholder, clearly temporary,
   never asserted-correct in tests until Checkpoint B lands.
2. **Wire in real streak query** (blocked on Checkpoint B): replace the
   placeholder with real `StreakQuery` calls per challenge.

**Verify:** integration tests seeding challenges/completions directly via
`AppDbContext`, asserting grouping/sorting (step 1, can be green before step
2); a follow-up test after step 2 asserting the endpoint plumbs the streak
query's result through correctly (not re-deriving the alive/dead rule itself).

---

## Stage 8 — Angular: Challenge feature (not blocked by either checkpoint)

**Status: Not started**

1. `features/challenges/challenge.model.ts`, `challenges.service.ts` —
   signals-based service per web/CLAUDE.md, `httpResource` for reads
   (matching `HealthStatus`'s pattern), `create()`/`archive()` methods that
   `reload()` the resource on success.
2. `challenge-list.ts` — standalone, `OnPush`, spartan `card` + cadence
   badge, archive button, `@if`/`@for`, loading/error states.
3. `challenge-form.ts` — reactive forms + spartan `input`/`select`/`field`
   + `toggle-group` for the fixed color palette. Generate missing spartan
   components via `ng g @spartan-ng/cli:ui` (consult the installed
   `spartan` skill, don't guess APIs). Surface FluentValidation's
   per-field problem-details errors on the form.
4. Add `/challenges` route in `app.routes.ts`.

**Verify:** `ng test`; manual add → list → archive round-trip against the
real backend from Stage 4 (`npm start` with proxy, or Angular CLI MCP
devserver tools).

---

## Stage 9 — Angular: Dashboard feature (blocked on Stage 7)

**Status: Not started**

1. `features/dashboard/dashboard.model.ts`, `dashboard.service.ts`
   (`httpResource` for `GET /api/dashboard`), `dashboard-page.ts` — open
   section (tick-off button per card) + collapsible "done this period"
   section, empty state linking to `/challenges`. Can be built against
   Stage 7's step-1 placeholder-streak shape without waiting on Checkpoint B.
2. Tick-off action wired to `POST /api/challenges/{id}/completions`, reload
   on success, surface RFC 7807 `detail` inline (not a generic toast).
3. Streak badge (blocked on Stage 7 step 2 / Checkpoint B) — minimal, e.g.
   spartan `badge` with count. No charts (heatmap is Phase 5).
4. Make Dashboard the `''` root route; move `HealthStatus` to `/health`
   (kept as a dev diagnostic). Plain `routerLink`s between the two — no
   full nav component needed for a single-user app.

**Verify:** `ng test`; manual end-to-end pass with both `dotnet run` and
`npm start` running: add daily/weekly/monthly challenges, tick each off,
confirm dashboard grouping/urgency/collapse per §6.4, confirm retroactive
bound enforcement is visible, confirm streak count once Checkpoint B lands.

---

## Stage 10 — Phase 2 close-out verification

**Status: Not started**

- `dotnet test` (full suite) green against real Postgres via Testcontainers.
- `ng test` green.
- Manual walkthrough covering all of §1's v1 scope statement end-to-end.
- Explicitly out of scope: auth (Phase 3), Compose/Caddy/CI (Phase 4),
  heatmap/OTel/PWA/backups (Phase 5), and all of §2's non-goals.

---

## Critical files referenced throughout

- `api/Api/Data/AppDbContext.cs` — currently empty, gets `DbSet`s + config wiring.
- `api/Api/Features/Health/HealthEndpoints.cs` / `HealthResponse.cs` — vertical-slice pattern to replicate.
- `api/Api.Tests/DatabaseSmokeTests.cs` — existing (unmodified) raw-Npgsql smoke test; new harness lives alongside it.
- `api/Api/Api.csproj`, `api/Directory.Packages.props` — package additions (EF Design, Mvc.Testing).
- `web/src/app/features/health/health-status.ts` — `httpResource` + spartan pattern to replicate.
- `web/src/app/app.routes.ts` — routing updates.
- `docs/domain.md`, `PROJECT-BRIEF.md` §5-§8 — authoritative domain rules for every DTO/validator/endpoint shape.

## Process notes for execution

- One scoped step per turn, per project convention — do not batch multiple
  numbered items above into a single implementation turn.
- Stages 3 and 6 are hard stops: present the checkpoint, get the human's
  file/decision, then resume — do not scaffold placeholder implementations
  inside the files the human owns.
