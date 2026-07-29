# Domain model and period logic

> Restates PROJECT-BRIEF.md §5–§8 as prose. If this file and the brief ever
> disagree, the brief wins — fix this file, don't reinterpret the brief.
> This file exists so a future session doesn't "correct" the period logic
> into something subtly wrong.

## Entities

**Challenge** — `Id, UserId, Name, Url?, Cadence, TargetCount, StartsOn, ArchivedAt?, Color, SortOrder`.

**Completion** — `Id, ChallengeId, PeriodStart, CompletedAt, Note?`, with a unique
constraint on `(ChallengeId, PeriodStart)`.

**User** — identity fields plus `TimeZoneId`, an IANA zone name (e.g.
`"Europe/Berlin"`). The time zone belongs to the user, not the challenge —
there is one clock per user, not one per challenge.

## `PeriodStart` vs `CompletedAt`

These record two different facts and must stay two different columns:

- **`PeriodStart`** (`DateOnly` / `date`) is the first day of the logical
  period a completion belongs to: the day itself for daily cadence, that ISO
  week's Monday for weekly, the 1st of the month for monthly. It is **data**,
  not a derived value — retroactive logging (see below) means the period a
  completion belongs to can differ from the day it was actually logged.
- **`CompletedAt`** (`DateTimeOffset` / `timestamptz`) is the instant the
  button was actually pressed.

`PeriodStart` must never be derived from `CompletedAt` at read time, and the
two must never be merged into one column.

`TargetCount` exists on `Challenge` but is always `1` in v1. It's a migration
path for a future "counts" feature (v3), not something to branch logic on now.

## The four decisions

### Weekly means calendar period, not rolling interval

A weekly challenge is satisfied once during a given ISO week (e.g.
2026-W31). Monday resets it. It is **not** "once every 7 days since the last
completion." This matches user expectation, gives completions a stable
unique key (`ChallengeId, PeriodStart`), and keeps the streak query
tractable. Weeks are ISO weeks, starting Monday.

### Completion is binary

A period is either done or not done — there is no partial credit.
`TargetCount` is always `1`; counts are out of scope until v3.

### Retroactive completion is allowed, but bounded

A user may tick off the current period or either of the previous two
periods. Anything older is rejected with an HTTP 400. The bound exists
because unbounded backfill would let a user fabricate a streak retroactively
— turning the tracker into "a lying machine."

### Weekly/monthly challenges appear on the dashboard every day

They stay visible every day until satisfied for the current period, then
collapse into a "done this period" section. While visible, they're sorted by
urgency — e.g. a weekly task still open on Sunday ranks above the same task
on Tuesday.

## The core function

```csharp
static DateOnly PeriodStartFor(DateTimeOffset instant, string timeZoneId, Cadence cadence)
```

This is the heart of the application: a pure function, deterministic for a
given `(instant, timeZoneId, cadence)` triple, with no database access, no
`DateTime.Now`, and no other ambient state. A bug here silently corrupts
every streak in the system, so it is tested before it is implemented.

Required test cases, and why each one exists:

- **29 March 2026, `Europe/Berlin`, 02:00** — the CET→CEST transition; this
  local time does not exist, and the function must still resolve to a
  sensible period.
- **25 October 2026, `Europe/Berlin`** — the CEST→CET transition; this local
  hour occurs twice, and both occurrences must resolve to the same period.
- **31 Dec 2026 → 1 Jan 2027, weekly** — an ISO week spanning the year
  boundary. Note **2026-W53 exists**; the transition must not skip or
  duplicate a week.
- **31 January, monthly cadence** — end-of-month boundary.
- **00:30 local time in Berlin** — must resolve to *today* (local), not
  *yesterday* (if computed naively in UTC).
- **23:30 local time in Berlin** — must resolve to *today* (local), not
  *tomorrow* (if computed naively in UTC).
- **A user in a non-European zone** (e.g. `Pacific/Auckland`) — proves
  nothing in the implementation is hardcoded to Europe or to a specific
  offset.

## Streak computation

Streaks are computed by converting each completion's `PeriodStart` into a
**period ordinal** — an integer that increments by exactly 1 per period —
and then solving gaps-and-islands over the resulting integer sequence:

- Daily: days since 1970-01-01.
- Weekly: (days since 1970-01-01 of that period's Monday) / 7.
- Monthly: `year * 12 + month`.

The reference query groups ordinals into consecutive runs (islands) and
picks the run ending at the highest ordinal:

```sql
WITH ordinals AS (
  SELECT (period_start - DATE '1970-01-01') / 7 AS ord   -- weekly example
  FROM completion WHERE challenge_id = @id
),
grouped AS (
  SELECT ord, ord - ROW_NUMBER() OVER (ORDER BY ord) AS grp
  FROM ordinals
)
SELECT COUNT(*) AS length, MAX(ord) AS ends_at
FROM grouped GROUP BY grp ORDER BY MAX(ord) DESC;
```

This is written as raw SQL via `db.Database.SqlQuery<T>()`, not as LINQ —
gaps-and-islands does not translate cleanly to LINQ/EF Core.

### The bug that will be written

The current period is not over yet, so a naive implementation will look at
the most recent island, see it doesn't reach "today's ordinal," and report
the streak as broken even though the user still has time left to complete
it.

**Correct rule:** the streak is alive if the most recent island ends at
either the *current* ordinal or the *previous* one. It only dies once the
current period has fully elapsed with no completion recorded. This needs an
explicit test.

Two more rules apply to streak computation:

- Periods before a challenge's `StartsOn` do not count as misses — the
  streak calculation only considers periods from `StartsOn` onward.
- Archived challenges (`ArchivedAt` set) freeze their streak at whatever it
  was, rather than letting it break as time passes after archiving.
