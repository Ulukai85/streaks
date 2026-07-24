# PROJECT BRIEF — streaks

> This file is the source of truth for project intent. It is written by the human,
> not the agent. The agent may read it, cite it, and derive other docs from it,
> but must not edit it without being explicitly asked.

---

## 1. What this is

A personal streak tracker. The user defines challenges ("eat no meat", "run 10 miles",
"do Wordle"), each with a cadence (daily / weekly / monthly). The dashboard shows what
is due in the current period and lets the user tick items off. Streaks are computed
from completion history.

**Scope of v1, verbatim:** add a challenge, see today's dashboard, tick it off, see the streak.

## 2. Non-goals (v1)

Do not build, propose, or scaffold any of the following. If a task seems to require
one of them, stop and ask instead.

- Reminders, push notifications, email
- Notes, quantities, partial credit, skip days, "freeze" tokens
- Categories, tags, search
- Charts beyond a single completion heatmap (and that is Phase 5, not now)
- Social features, sharing, multi-user collaboration
- Mobile native apps
- Import/export

## 3. Architectural non-goals

This is a four-table CRUD app with one interesting algorithm. The following are
explicitly rejected for v1 and must not be introduced:

- MediatR / CQRS / event sourcing
- Clean Architecture with more than 2 projects (`Api`, `Api.Tests`)
- NgRx or any external state library
- Microservices, message queues, GraphQL
- Repository pattern wrapping EF Core (`DbContext` is already the abstraction)

**Rule: no new NuGet or npm dependency without an ADR in `docs/decisions/`.**
Three sentences: what problem, what alternative was considered, why this.

---

## 4. Stack (locked)

| Layer | Choice                                                                                   |
|---|------------------------------------------------------------------------------------------|
| API | .NET 10, Minimal APIs, grouped by feature                                                |
| Language | C#, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` |
| Data | EF Core + PostgreSQL                                                                     |
| Time | `DateTimeOffset` and `DateOnly` only. **`DateTime` is banned.**                          |
| Validation | FluentValidation                                                                         |
| Frontend | Angular 22, standalone components, signals, `@if` / `@for`, `httpResource`               |
| State | Signals in a plain service                                                               |
| Tests | xUnit + Testcontainers (real PostgreSQL, never in-memory, never SQLite)                  |
| Deploy | Docker Compose + Caddy on a Hetzner VPS                                                  |

---

## 5. Domain model

```
Challenge   (Id, UserId, Name, Url?, Cadence, TargetCount, StartsOn,
             ArchivedAt?, Color, SortOrder)

Completion  (Id, ChallengeId, PeriodStart, CompletedAt, Note?)
            UNIQUE (ChallengeId, PeriodStart)

User        (Id, ...identity..., TimeZoneId)   -- IANA, e.g. "Europe/Berlin"
```

### Field semantics — read carefully

- **`PeriodStart`** is a `DateOnly` (`date` in PG). It is the **first day of the logical
  period the completion belongs to**. Daily → the day itself. Weekly → that ISO week's
  Monday. Monthly → the 1st of the month.
- **`CompletedAt`** is a `DateTimeOffset` (`timestamptz`). It records **when the button
  was actually pressed**.

These are two different facts. **They must never be merged into one column, and
`PeriodStart` must never be derived from `CompletedAt` at read time.** The period is
data, because retroactive logging is allowed (see §6.3).

- **`TargetCount`** exists but is always `1` in v1. It is a migration path, not a feature.
  Do not write logic that branches on it.
- **`TimeZoneId`** lives on the User, not the Challenge.

---

## 6. The four decisions

These were decided deliberately. Do not revisit them in code review or "improve" them.

### 6.1 "Weekly" means calendar period, not rolling interval
Once during ISO week 2026-W31. Monday resets it. **Not** "once every 7 days since last
completion." Rationale: matches user expectation, gives a stable unique key, keeps
streak logic tractable. ISO weeks, Monday start.

### 6.2 Completion is binary
Done or not done. `TargetCount` is always 1. Counts are a v3 concern.

### 6.3 Retroactive completion is allowed, but bounded
The user may tick off the current period plus the **previous two**. Anything older is
rejected with a 400. Unbounded backfill turns a streak tracker into a lying machine.

### 6.4 Weekly/monthly challenges appear on the dashboard every day
They stay visible until satisfied, then collapse into a "done this period" section.
Sort by urgency: a weekly task on Sunday ranks above the same task on Tuesday.

---

## 7. The core function

```csharp
static DateOnly PeriodStartFor(DateTimeOffset instant, string timeZoneId, Cadence cadence)
```

Pure. No database, no `DateTime.Now`, no ambient state, no I/O. Deterministic for a
given input triple. This is the heart of the application — a bug here silently corrupts
every streak in the system.

### Required test cases (write these before the implementation)

- **29 March 2026**, `Europe/Berlin`, 02:00 — CET→CEST transition, the hour that does not exist
- **25 October 2026**, `Europe/Berlin` — CEST→CET, the hour that occurs twice
- **31 Dec 2026 → 1 Jan 2027**, weekly — ISO week spanning the year boundary; note that
  **2026-W53 exists**
- **31 January**, monthly cadence
- **00:30 local time in Berlin** — must resolve to *today* local, not yesterday UTC
- **23:30 local time in Berlin** — must resolve to *today*, not tomorrow UTC
- A user in a non-European zone (e.g. `Pacific/Auckland`) to prove nothing is hardcoded

Decide via ADR whether to use NodaTime or `TimeZoneInfo` with IANA IDs.

---

## 8. Streak computation

Convert `PeriodStart` to a **period ordinal** — an integer that increments by exactly 1
per period — then treat it as gaps-and-islands.

- Daily → days since 1970-01-01
- Weekly → (days since 1970-01-01 of that Monday) / 7
- Monthly → `year * 12 + month`

```sql
WITH ordinals AS (
  SELECT (period_start - DATE '1970-01-01') / 7 AS ord   -- weekly
  FROM completion WHERE challenge_id = @id
),
grouped AS (
  SELECT ord, ord - ROW_NUMBER() OVER (ORDER BY ord) AS grp
  FROM ordinals
)
SELECT COUNT(*) AS length, MAX(ord) AS ends_at
FROM grouped GROUP BY grp ORDER BY MAX(ord) DESC;
```

### The bug that will be written

The current period is not over yet, so a naive implementation reports the streak as broken.

**Correct rule:** the streak is alive if the most recent island ends at either the
*current* ordinal or the *previous* one. It dies only once the current period has fully
elapsed with no completion. There must be an explicit test for this.

Also required:
- Periods before `StartsOn` do not count as misses.
- Archived challenges freeze their streak rather than breaking it.

Write the streak query as **raw SQL** via `db.Database.SqlQuery<T>()`. Do not attempt
to express gaps-and-islands in LINQ.

---

## 9. Conventions

- `AsNoTracking()` on every read path. No exceptions in v1.
- `Cadence` is persisted **as a string**, not an int.
- Sensitive-data logging enabled in Development; generated SQL is expected to be read.
- Minimal API endpoints grouped by feature in `api/src/Features/<Feature>/`.
- No repository abstraction over EF Core; no service class that only forwards; query classes for non-trivial reads
- Vertical slices: each feature folder contains its own endpoints, DTOs, validators
- Integration tests hit a real PostgreSQL via Testcontainers.
- Angular: standalone components only, signals for state, no `NgModule`.
- Errors: RFC 7807 problem details.
- Code and docs in English. UI text in German. No localization in v1.

---

## 10. Working agreement

**The human writes, by hand:**
- `PeriodStartFor` and its test suite
- The streak SQL and its tests

If asked to implement either, push back and confirm first. These are the reason the
project exists; generating them defeats its purpose.

**The agent writes:**
- CRUD endpoints, DTOs, validators, EF configuration, migrations
- Angular components, services, routing, guards, interceptors
- Dockerfiles, Compose, Caddyfile, CI pipeline
- Test scaffolding (but not the period/streak assertions)

**Process:**
- Plan before implementing anything non-trivial; the human reads the plan first.
- One scoped task per turn. Not "build challenge management."
- Stop and ask rather than inventing a domain rule not stated here.

---

## 11. Phases

| Phase | Content |
|---|---|
| 1 | Walking skeleton: one endpoint, one page, real Postgres, one integration test, deployable. No auth. |
| 2 | Domain: challenges, completions, `PeriodStartFor`, streaks. **Tests first.** Two weekends. |
| 3 | Auth: ASP.NET Core Identity bearer tokens, Angular interceptor, refresh flow, route guard. ADR on token storage before coding. |
| 4 | Ship to Hetzner: Compose, Caddy, GitHub Actions on push to main. Then use it daily for two weeks. |
| 5 | Heatmap, OTel, PWA manifest, backups with a verified restore. |

---

## 12. First task

Do **not** write application code yet. Produce these four files and stop:

1. `docs/domain.md` — §5, §6, §7, §8 of this brief restated as prose, including the DST
   examples and the streak edge case. This is the file that prevents a future session
   from "correcting" the period logic into something subtly wrong.
2. `docs/decisions/0001-period-model.md` — ADR: calendar periods over rolling intervals.
3. `api/CLAUDE.md` — backend rules: no `DateTime`, nullable-as-errors, `AsNoTracking()`,
   Testcontainers, no repository pattern, ADR-before-dependency.
4. `web/CLAUDE.md` — frontend rules: standalone + signals, no NgRx, no `NgModule`.

Then propose (do not create) a trimmed root `CLAUDE.md` under 100 lines that points at
`docs/` rather than duplicating it.
