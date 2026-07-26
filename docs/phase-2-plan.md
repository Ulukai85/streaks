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
  fields configured yet — Phase 3 extends this same table with ASP.NET Core
  Identity fields rather than replacing it (`User : IdentityUser<Guid>` from
  Stage 1 onward, see ADR 0007). One hardcoded dev user seeded via code at
  startup (dev environment only, not `HasData` — see ADR 0004 addendum),
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

**Status: Done**

Use the existing (currently empty) `Api/Domain/` folder for entities and enums.

1. `Cadence` enum (`Api/Domain/Cadence.cs`): `Daily, Weekly, Monthly`, mapped
   via `HasConversion<string>()` → persisted as `"Daily"/"Weekly"/"Monthly"`.
   Must exist before Checkpoint A since the human's `PeriodStartFor` signature
   depends on it.
2. `User` entity (`Api/Domain/User.cs`: `User : IdentityUser<Guid>` + `TimeZoneId`,
   see ADR 0007) + EF config (`Api/Data/Configurations/UserConfiguration.cs`).
   Dev user is seeded via code at startup, not `HasData` (see ADR 0004
   addendum — `HasData` bakes seed rows into the migration as compile-time
   constants, which fights the Identity fields `User` gains in Phase 3). Add
   `DbSet<User> Users` to `AppDbContext`, wire `ApplyConfigurationsFromAssembly`
   in `OnModelCreating`.
3. `Challenge` entity + config: FK to `User`, `Cadence` as string,
   index on `(UserId, ArchivedAt)`.
4. `Completion` entity + config: FK to `Challenge`, **unique index on
   `(ChallengeId, PeriodStart)`** (DB-enforced per ADR 0003).
5. ~~Generate migrations per entity~~ — generate a single `InitialCreate`
   migration once all four entities and their configs exist. Migrations
   represent schema deltas to deploy, not entities defined; nobody will ever
   apply the `User` table without `Challenge`/`Completion`.

**Verify:** `dotnet build` clean; migration generation produces no pending
model changes; generated migration SQL confirmed to declare `PeriodStart`
and `StartsOn` as `date` (grepped from the migration file); migration applied
against local Postgres (`dotnet run`), confirming the seed row inserts once
and stays at one row across a restart; manual psql check that the unique
index on `(ChallengeId, PeriodStart)` rejects a duplicate insert and that
deleting a `Challenge` with existing `Completion` rows fails with a
foreign-key violation (not a cascade) — per ADR 0003's "test the constraint,
not the code that avoids it." Stage 2's automated integration tests still
need to assert the same constraints once the test harness exists.

---

## Stage 2 — Integration test harness (blocks all later backend tests)

**Status: Done**

`Api.Tests/Infrastructure/` now holds the shared harness, alongside the
pre-existing `ValidationFilterTests.cs`; `DatabaseSmokeTests.cs` (raw-Npgsql,
ad-hoc container) is unmodified and coexists, out of scope.

- `PostgresFixture.cs`: `IAsyncLifetime` wrapping
  `PostgreSqlBuilder("postgres:18-alpine")`, shared via
  `ICollectionFixture<PostgresFixture>` (one `[CollectionDefinition]` in
  `IntegrationTestCollection.cs`, referenced everywhere by its `const string
  Name`, never a literal). On init, runs its own throwaway
  `AppDbContext.Database.MigrateAsync()` against the container.
  `ResetDatabaseAsync()` truncates `Completions`/`Challenges` together
  (satisfies the `Restrict` FK without `CASCADE`), leaves `Users` alone, and
  throws if `Users` doesn't come out to exactly 1 row afterward — a future
  test that creates a second user fails loudly instead of leaking it.
- `ApiFactory.cs`: `WebApplicationFactory<Program>`, constructor takes
  `PostgresFixture` (a supported class-fixture-depends-on-collection-fixture
  pattern in xUnit v2 — collection-fixture-to-collection-fixture is *not*
  supported, which is why `ApiFactory` is an `IClassFixture`, not a second
  `ICollectionFixture`, rebuilding the lightweight host per test class while
  the container itself stays shared). `ConfigureWebHost` sets
  `Environment = "Development"` (so `Program.cs`'s real seed logic fires) and
  overrides `AppDbContext`'s registration via `ConfigureTestServices` +
  `RemoveAll<DbContextOptions<AppDbContext>>()` to point at the container,
  never the real dev DB. Added `Microsoft.AspNetCore.Mvc.Testing` 10.0.10
  (ADR 0003 addendum) plus an explicit `Microsoft.EntityFrameworkCore.Relational`
  10.0.10 pin (resolved a transitive version-conflict warning from the
  Npgsql provider's own dependency range).
- `IntegrationTestBase.cs`: abstract base combining both — exposes `Client`,
  a `CreateDbContext()` factory (fresh instance per call, so arrange and
  assert never share one change tracker), and a `ResetDatabase()` wrapper for
  tests that want to trigger a second, mid-test reset explicitly; resets the
  DB in `InitializeAsync()` before every test method automatically.
- `DatabaseHarnessTests.cs`: proves the harness itself — dev-user round-trip,
  a real HTTP call through the pipeline, a self-contained test that inserts a
  `Challenge`, calls `ResetDatabase()` again mid-test, and asserts it's gone
  (proves reset directly, without depending on xUnit's execution order
  between two separate tests), and an EF-level duplicate-`(ChallengeId, PeriodStart)`
  insert asserting `DbUpdateException` through the full stack.

**Verify:** `dotnet test` green (8/8, full suite); confirmed via `docker ps`
sampling that exactly one ephemeral Postgres container serves the whole
`DatabaseHarnessTests` run; confirmed a typo'd `[Collection(...)]` reference
fails at **compile time** (`xUnit1041` analyzer error), not just silently —
stronger than the const-symbol mitigation alone.

---

## Stage 3 — HUMAN CHECKPOINT A: `PeriodStartFor`

**Status: Done**

**Hard stop.** Agent does not write the function, file, or tests. Confirm:
- Signature per §7, using the `Cadence` enum from Stage 1.
- Location: `Api/Domain/PeriodCalculator.cs`, static class `PeriodCalculator`.
- NodaTime vs `TimeZoneInfo`/IANA — human's ADR + implementation.

**Blocked on this:** Completion tick-off (Stage 5.2), Dashboard endpoint
(Stage 7). **Not blocked:** Challenge CRUD (Stage 4) and its UI (Stage 8) —
zero dependency on period math, proceed in parallel.

---

## Stage 4 — Challenge CRUD endpoints (parallel to Stage 3)

**Status: Done**

Vertical slice under `Api/Features/Challenges/`, pattern-matched against
`Features/Health/`.

- `ICurrentUserProvider` (`Api/Infrastructure/`) introduced ahead of Phase 3:
  endpoints inject it instead of reading a hardcoded Guid, so wiring up real
  auth later is a one-line DI swap. `DevSeed.UserId` is now `internal` (was
  `private`) so `DevCurrentUserProvider` can read it; still not visible to
  `Api.Tests` — tests get the seeded user via `db.Users.Single()`, per the
  Stage 2 precedent.
- `CreateChallengeRequest`/`ChallengeResponse`/`CreateChallengeRequestValidator`:
  name required, cadence must parse (case-insensitive), color checked
  against `Api/Domain/ChallengeColors.cs` (single source of truth: `red,
  orange, amber, green, teal, blue, indigo, pink`), URL — when non-blank —
  must be an absolute `http`/`https` URL (rejects `javascript:`/`file:`/relative
  paths). `Cadence` travels as a string on both request and response,
  matching how it's persisted. `TargetCount` is not client-settable —
  server always persists `1`. `SortOrder` is optional; omitted means
  append-at-end (current max for that user + 1, or `0`). `StartsOn` is not
  client-settable either — computed server-side via
  `PeriodCalculator.PeriodStartFor(timeProvider.GetUtcNow(), user.TimeZoneId, Cadence.Daily)`,
  reusing the Stage 3 function as the one definition of "today" (required
  registering `TimeProvider` in DI — no endpoint calls `DateTimeOffset.UtcNow` directly).
- `POST /api/challenges/` — `201 Created` with a real `Location` header
  (`TypedResults.Created`), not a bare status code.
- `GET /api/challenges/` — `AsNoTracking()`, non-archived only, ordered by
  `SortOrder`.
- `POST /api/challenges/{id}/archive` — idempotent: already-archived
  returns `200` + the existing state (not re-stamped, not an error);
  unknown/other-user id returns `404`.

No general update endpoint — brief's v1 scope only lists create + tick-off;
archive is the only other listed mutation.

**Verify:** `dotnet test --filter Challenges` green (26 validator unit
tests + 7 integration tests, including an empty-`Name` → `400` RFC 7807
case and an idempotent-archive-twice case); full suite still green. One
real finding along the way: comparing `ArchivedAt` across two HTTP
round-trips needs `BeCloseTo`, not exact equality — Postgres `timestamptz`
truncates to microsecond precision while .NET `DateTimeOffset` ticks are
100ns, so a value read back from the DB loses the last digit of precision
versus the in-memory value from the initial write.

---

## Stage 5 — Completion tick-off endpoint

**Status: Done**

`POST /api/challenges/{challengeId}/completions`, vertical slice under
`Api/Features/Completions/`.

- The bounds check does **not** use cadence-specific date-stepping
  (`AddDays`/`AddMonths`), which would have reimplemented brief §8's period
  math as a second, independent source of truth. Instead `Api/Domain/PeriodOrdinal.cs`
  (`static int For(DateOnly periodStart, Cadence cadence)`) transcribes §8's
  ordinal formula directly (daily = days since 1970-01-01, weekly = that / 7,
  monthly = `year * 12 + month`), and the bounds check is one integer
  comparison: `periodsAgo = PeriodOrdinal.For(current) - PeriodOrdinal.For(given)`,
  valid iff `periodsAgo` is `0`, `1`, or `2`. This is a new agent-owned file
  — it has zero timezone/DST logic and only operates on an already-resolved
  `DateOnly` — not an edit to the human-owned `PeriodCalculator.cs`, and not
  literally shared code with Stage 6 (the streak SQL is hand-written raw SQL
  in a different execution context; it will encode the same §8 formula
  directly).
- Future `PeriodStart` values are rejected by the same check
  (`periodsAgo < 0` → `400`), no separate special case.
- A given `PeriodStart` that isn't the canonical start of its period (not a
  Monday for weekly, not the 1st for monthly) is rejected as `400` before
  the ordinal diff even runs — otherwise a misaligned date can land in a
  neighboring integer-division bucket and pass/fail the bounds check for
  the wrong reason.
- Archived challenges reject new completions with `409 Conflict` (inferred
  from §8's "archived challenges freeze their streak" — not stated
  explicitly, confirmed this session).
- Duplicate `(ChallengeId, PeriodStart)` → `409 Conflict`, mapped by a new
  global `Api/Infrastructure/UniqueConstraintExceptionHandler.cs`
  implementing the BCL `IExceptionHandler`, registered via
  `AddExceptionHandler<T>()` ahead of `AddProblemDetails()`. Matches
  `DbUpdateException` wrapping `Npgsql.PostgresException` with
  `SqlState == PostgresErrorCodes.UniqueViolation` ("23505"); anything else
  falls through to the pre-existing default 500/problem-details behavior.
  First exception handler in the app — the pattern is reusable for any
  future unique constraint without a per-endpoint catch block.
- `CompletionResponse` carries `Id, ChallengeId, PeriodStart, CompletedAt,
  Note` — enough for the dashboard to update optimistically without a
  second round trip. `Note` max length 500 (not specified in the brief,
  confirmed this session); blank `Note` persists as `null`, mirroring
  Stage 4's `Url` handling.
- **Post-Stage-6 fix:** the `periodsAgo` bounds check alone let a
  brand-new challenge accept a completion for a period before it existed
  (still within "current ± 2" even though before `Challenge.StartsOn`) —
  found while reviewing Stage 6's `StreakQuery` (its own `PeriodStart >=
  startsOn` SQL filter protected the streak math from it, but the bad
  completion still got persisted). Fixed with a second, explicit ordinal
  comparison against `challenge.StartsOn` alongside the existing bound
  check. `Post_Accepts_PeriodStart_Within_Bound` had to start backdating
  the test challenge's `StartsOn` directly via `AppDbContext` — it had been
  passing only because every freshly-created test challenge's `StartsOn`
  defaults to "now," which happened to mask this exact gap.

**Verify:** `dotnet test --filter "Completion|PeriodOrdinal"` green (24
tests: `PeriodOrdinal` unit tests, validator unit tests, and integration
tests covering happy path, in-bound/out-of-bound/future/misaligned
`PeriodStart`, duplicate-completion `409`, archived-challenge `409`, and
unknown-challenge-id `404` — asserting exact status codes throughout, not
just non-200); full suite green (72/72). Manual `dotnet run` + `curl` smoke
pass against the real dev DB confirmed the exception-handler wiring
end-to-end (create → complete `201` → duplicate `409` → future date `400`
→ unknown id `404` → archive then complete `409`), then cleaned up the
throwaway rows. Full suite green again (106/106) after the post-Stage-6 fix
above, including a new `Post_Rejects_PeriodStart_Before_Challenge_StartsOn`
regression test.

---

## Stage 6 — HUMAN CHECKPOINT B: streak SQL (parallel to Stages 3-5)

**Status: Done**

Written by the human per the off-limits rule. The agent implemented the test
suites afterward on the human's explicit instruction (a deliberate, confirmed
exception to "including their test suites" in the off-limits note below —
not a standing precedent for future checkpoints).

- `Api/Features/Streaks/StreakQuery.cs`/`StreakRow.cs`/`StreakResult.cs`.
  Result contract: `StreakResult(int Length, bool IsAlive, DateOnly?
  LastCompletedPeriod)`.
- One query shared across all three cadences via `CASE {cadence} WHEN
  'Daily' THEN ... END AS ord` inside the SQL itself, rather than three
  near-duplicate query strings — keeps the query always-valid, complete SQL
  so `//language=sql` highlighting/parsing keeps working (an earlier draft
  templated the `ord` expression in via string `Replace()`, which broke it).
- Filters `WHERE "PeriodStart" >= {startsOn}` directly in the SQL, so periods
  before a challenge's `StartsOn` are excluded from the gaps-and-islands
  computation itself, not just patched around afterward.
- Archived challenges freeze via `archivedAtLocalDateOnly ?? today`: the
  caller passes `ArchivedAt` already converted to the user's local period
  start, and the alive/dead rule is evaluated as of that instant forever
  after, never against the real clock.
- Two EF Core `SqlQuery<T>` gotchas surfaced during review and are now noted
  in `api/CLAUDE.md` for any future raw-SQL work: a trailing `;` inside the
  raw SQL breaks `.FirstOrDefaultAsync()`'s query composition, and result
  column aliases must match the target record's property names exactly (no
  snake_case translation without the unused `EFCore.NamingConventions`
  package).
- One real bug caught along the way: an early draft derived
  `LastCompletedPeriod` from `MAX("CompletedAt")` run back through
  `PeriodStartFor` — reintroducing the "`PeriodStart` derived from
  `CompletedAt` at read time" mistake `docs/domain.md` warns against, wrong
  whenever an older period in the same island is backfilled *after* a newer
  one was completed on time. Fixed by carrying `PeriodStart` itself through
  the query (`MAX("PeriodStart")`) instead of touching `CompletedAt`.

**Verify:** `Api.Tests/Features/Streaks/StreakQueryTests.cs` — pure C#, no
database — hand-transcribes the SQL's ordinal arithmetic independently of
`PeriodOrdinal.For` and asserts the two agree across a spread of dates,
including the 2026-W53→2027-W01 ISO year boundary (12 cases).
`StreakQueryBehaviorTests.cs` — Testcontainers, seeding `Challenge`/
`Completion` directly via `AppDbContext` — exercises the alive/dead rule
against real Postgres: current-period alive, previous-period grace alive,
dead at exactly a 2-period gap, a brand-new zero-completion challenge reads
as fresh rather than broken, archived-freeze alive/dead evaluated against
`ArchivedAt` rather than the clock, and a regression case for the
`LastCompletedPeriod` bug above (21 cases, 7 scenarios × 3 cadences). Full
suite green: 105/105.

---

## Stage 7 — Dashboard read endpoint

**Status: Done**

Built as one unified implementation rather than the original two-step
(placeholder-then-real-streak) split — that split only existed because
Checkpoint B hadn't landed when this stage was originally planned; it had by
the time this stage was implemented, so `StreakQuery` was wired in from the
start. Confirmed with the human before implementing, along with three other
decisions below.

- `GET /api/dashboard/` — `Api/Features/Dashboard/DashboardEndpoints.cs` +
  `DashboardResponse` (`Open: [...]`, `DoneThisPeriod: [...]`). Loads the dev
  user's non-archived, started challenges (`AsNoTracking()`, `StartsOn <=
  today`), computes each one's current period start per cadence, checks for
  a matching `Completion` to split open vs. done, sorts `Open` by
  days-remaining-in-period ascending (urgency, §6.4) then `SortOrder`, and
  `DoneThisPeriod` by `SortOrder` alone (not specified in the brief —
  confirmed this session, matches `GET /api/challenges/`'s existing default).
- New `Api/Domain/PeriodUrgency.cs` (`DaysRemaining(DateOnly periodStart,
  DateOnly today, Cadence cadence)`) — agent-owned pure calendar math, same
  category as `PeriodOrdinal.cs` from Stage 5: no timezone/DST logic, not a
  touch on the off-limits `PeriodStartFor`. Monthly's end-of-month is
  computed via `DateOnly` chaining (`AddMonths(1).AddDays(-1)`), not
  `DateTime.DaysInMonth`, to stay clear of the "no `DateTime`" hard rule
  entirely rather than argue whether a static BCL call counts.
- Avoids per-challenge completion-lookup queries: at most 3 distinct cadences
  per request means at most 3 `PeriodCalculator` calls, then one batched
  `Completions` query (`ChallengeId IN (...) AND PeriodStart IN (...)`)
  matched in-memory per challenge via a `HashSet<(Guid, DateOnly)>`. The
  per-challenge `StreakQuery.ForChallenge` call remains N separate calls —
  that's the already-accepted Stage 6 pattern, out of scope to batch here.
- Response DTO stays minimal (`Id, Name, Url, Cadence, Color, SortOrder,
  Streak`) — no `PeriodStart`/`DaysRemaining` on the wire, since nothing in
  Stage 8/9's UI plan displays either (confirmed this session; trivial to add
  later if that changes). `Streak` is `DashboardStreakResponse`, a small
  Dashboard-owned record mapped from `StreakResult` rather than serializing
  `Api.Features.Streaks.StreakResult` directly — keeps each feature owning
  its own response contract per `docs/aspnet-conventions.md` (confirmed this
  session).
- `archivedAtLocalDateOnly` is always `null` when `StreakQuery.ForChallenge`
  is called from here — the `Where` clause already excludes archived
  challenges, so a dashboard-loaded challenge is never archived. Accepted
  fact, not a workaround: the parameter exists for a (currently nonexistent)
  future archived view.

**Verify:** `Api.Tests/Domain/PeriodUrgencyTests.cs` — pure C#, no database —
covers daily (always `0`), weekly (Monday/mid-week/Sunday), and monthly
(first day, last day, and a leap-year-vs-non-leap-year February pair proving
`AddMonths(1).AddDays(-1)` is actually leap-aware) (10 cases).
`Api.Tests/Features/Dashboard/DashboardEndpointsTests.cs` — Testcontainers,
seeding `Challenge`/`Completion` directly via `AppDbContext` — covers empty
groups, archived-exclusion, not-yet-started-exclusion, open/done grouping
(including a weekly challenge completed only last period, still open this
week), `DoneThisPeriod`'s `SortOrder` ordering, `Open`'s `SortOrder` tie-break
for same-cadence items, cross-cadence urgency ordering (daily always ranks
first), and two streak-wiring tests asserting the endpoint plumbs
`StreakQuery`'s result through correctly rather than re-deriving the
alive/dead rule (11 cases). No fake/controllable `TimeProvider` was added —
every ordering test is designed to hold regardless of the real calendar date
at run time (same-cadence ties, or a cadence whose urgency is always the
global minimum), consistent with this codebase's existing convention of
recomputing expected values from real `DateTimeOffset.UtcNow` rather than
faking the clock. Full suite green: 127/127. Manual `dotnet run` + `curl`
smoke test against the real dev DB confirmed grouping and streak wiring
end-to-end (daily challenge ticked off → `doneThisPeriod` with `length: 1,
isAlive: true`; untouched weekly challenge → `open` with a fresh `length: 0`
streak), then cleaned up the throwaway rows.

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

## Stage 9 — Angular: Dashboard feature (unblocked — Stage 7 is done)

**Status: Not started**

1. `features/dashboard/dashboard.model.ts`, `dashboard.service.ts`
   (`httpResource` for `GET /api/dashboard`), `dashboard-page.ts` — open
   section (tick-off button per card) + collapsible "done this period"
   section, empty state linking to `/challenges`.
2. Tick-off action wired to `POST /api/challenges/{id}/completions`, reload
   on success, surface RFC 7807 `detail` inline (not a generic toast).
3. Streak badge — minimal, e.g. spartan `badge` with count. No charts
   (heatmap is Phase 5).
4. Make Dashboard the `''` root route; move `HealthStatus` to `/health`
   (kept as a dev diagnostic). Plain `routerLink`s between the two — no
   full nav component needed for a single-user app.

**Verify:** `ng test`; manual end-to-end pass with both `dotnet run` and
`npm start` running: add daily/weekly/monthly challenges, tick each off,
confirm dashboard grouping/urgency/collapse per §6.4, confirm retroactive
bound enforcement is visible, confirm streak count is correct.

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
