# ADR 0003 — Integration tests run against real PostgreSQL via Testcontainers

Date: 2026-07-24
Status: Accepted

## Context

EF Core ships two easy substitutes for a real database in tests: the InMemory
provider and SQLite. Both start faster than a containerized Postgres and need no
Docker daemon. The stack table in `PROJECT-BRIEF.md` (§4) already locks Testcontainers
with real PostgreSQL as the only option; this ADR records why, since "just use
InMemory" is the default suggestion in most EF Core tutorials and would otherwise get
reintroduced by habit.

## Decision

All integration tests run against real PostgreSQL, started per test run via
Testcontainers. The InMemory provider and SQLite are not used anywhere in this
project, including unit-style tests of query classes.

## Rationale

- **The streak query is raw SQL** (`db.Database.SqlQuery<T>()`, see `docs/domain.md`
  §8), using Postgres-specific integer division and window functions. Neither
  InMemory nor SQLite executes this SQL at all, or executes a dialect close enough to
  mean anything — a passing test against either would not test the actual query.
- **The unique constraint on `(ChallengeId, PeriodStart)`** (see `docs/domain.md` §5)
  is enforced by the database. InMemory does not enforce relational constraints;
  SQLite's constraint and type-affinity behavior diverges from Postgres. A test suite
  built on either would pass while the real database rejects — or silently accepts —
  data the tests never saw.
- **`DateOnly`/`DateTimeOffset` column mapping** is provider-specific. Testing against
  a different engine than production risks a mapping bug that only surfaces in
  Postgres, i.e. in production.
- The cost — slower test startup, a Docker daemon requirement — is accepted because a
  green test suite that doesn't reflect production behavior is worse than no test
  suite: it's the false confidence this project is explicitly trying to avoid (see
  ADR 0002, where the same reasoning rules out mocked repositories).

## Consequences

- Local test runs and CI both require a working Docker daemon.
- Test suite startup is slower than InMemory/SQLite; accepted in exchange for tests
  that mean something.
- No test anywhere in the solution may reference the EF Core InMemory provider or a
  SQLite connection string. Any PR introducing one is rejected without further
  discussion — this was already decided.

## Alternatives considered

- **EF Core InMemory provider** — rejected: doesn't enforce constraints, doesn't run
  raw SQL, doesn't validate provider-specific type mapping.
- **SQLite** — rejected: closer to a real engine than InMemory, but still diverges
  from Postgres on constraint behavior, date/time handling, and does not support the
  raw SQL the streak query depends on.
