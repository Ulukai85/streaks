# Phase 2 — Domain (challenges, completions, streaks) + minimal UI

> Historical record for Phase 2 (see `PROJECT-BRIEF.md` §11) — all 10 stages
> are done, so this is kept short rather than as a live working plan. See
> `docs/phase-3-plan.md` for what's still ahead.

## Outcome

All of PROJECT-BRIEF §1's v1 scope — add a challenge, see today's dashboard,
tick it off, see the streak — works end-to-end without auth. Backend: .NET
10 Minimal API + EF Core/PostgreSQL, four entities (`User`/`Challenge`/
`Completion` + `Cadence` enum), CRUD + tick-off + dashboard endpoints, the
gaps-and-islands streak SQL. Frontend: Angular 22 with a Challenges page and
a Dashboard page, both signals/`httpResource`-based. `PeriodStartFor`
(`Api/Domain/PeriodCalculator.cs`) and the streak SQL
(`Api/Features/Streaks/StreakQuery.cs`) were written by hand at two human
checkpoints (Stages 3 and 6) — see `docs/domain.md` for what they do and why.

## Real bugs and gotchas found along the way

Kept as a durable record — patterns that could recur if similar code is
touched again:

- **`StreakQuery`'s `LastCompletedPeriod`** originally derived from
  `MAX("CompletedAt")` rather than `MAX("PeriodStart")`, reintroducing the
  "`PeriodStart` derived from `CompletedAt`" mistake `docs/domain.md` warns
  against — breaks as soon as an older period is backfilled after a newer
  one was completed on time.
- **`Challenge.StartsOn` used `Cadence.Daily` unconditionally at creation**
  instead of the challenge's own cadence — invisible for Daily challenges
  (today's date already is the correct period start), but broke Weekly
  (completions rejected) and Monthly (completion succeeded, streak silently
  stayed 0). Slipped past 127 passing tests because the only `StartsOn`
  assertion in the suite happened to use a Daily challenge.
- **Dashboard showed stale data after navigating away and back** —
  `DashboardService`'s `httpResource` fetches once per app lifetime;
  creating a challenge elsewhere didn't invalidate it. Fixed with an
  explicit `.reload()` in `DashboardPage`'s constructor.
- **Signal Forms left stale validation errors visible after a successful
  submit** — `submit()` marks fields `touched` regardless of outcome, and
  resetting only the model value left `touched` set on now-empty required
  fields. Fixed with `field().reset(emptyModel())`, which clears both.
- **Testing gotchas**: comparing a `DateTimeOffset` written and then read
  back from Postgres needs `BeCloseTo`, not exact equality — `timestamptz`
  truncates to microsecond precision, .NET ticks are 100ns. `httpResource`'s
  fetch is driven by an `effect()` that only runs on an actual
  `ApplicationRef.tick()`/`fixture.detectChanges()`, not by awaiting
  microtasks alone. Two `db.Database.SqlQuery<T>()` gotchas (trailing `;`
  breaking query composition, exact-match column aliasing) are recorded
  permanently in `api/CLAUDE.md`.

## Stages

| Stage | What | Status |
|---|---|---|
| 0 | Cross-cutting backend setup (`ValidationFilter`, problem details) | Done |
| 1 | Domain entities, EF config, migrations, seed user | Done |
| 2 | Integration test harness (Testcontainers + `WebApplicationFactory`) | Done |
| 3 | Human checkpoint: `PeriodStartFor` | Done |
| 4 | Challenge CRUD endpoints | Done |
| 5 | Completion tick-off endpoint | Done |
| 6 | Human checkpoint: streak SQL | Done |
| 7 | Dashboard read endpoint | Done |
| 8 | Angular: Challenge feature | Done |
| 9 | Angular: Dashboard feature | Done |
| 10 | Phase 2 close-out verification | Done |

**Phase 2 is complete.**
