# ADR 0004 — EF Core + Npgsql provider, FluentValidation

Date: 2026-07-24
Status: Accepted

## Context

`PROJECT-BRIEF.md` §4 already locks the stack: EF Core + PostgreSQL for data
access, FluentValidation for request validation. This ADR is not re-opening
either choice — it records the concrete packages that implement them, per the
project rule that no new NuGet dependency lands without an ADR (see ADR 0003,
which does the same for Testcontainers despite also being stack-locked).

## Decision

- `Microsoft.EntityFrameworkCore` + `Npgsql.EntityFrameworkCore.PostgreSQL` —
  the only maintained EF Core provider for PostgreSQL, and the one the whole
  domain model (`DateOnly`/`DateTimeOffset` mapping, raw SQL streak query via
  `db.Database.SqlQuery<T>()`) is designed around.
- `FluentValidation` — request validation for Minimal API endpoints. Not
  `FluentValidation.AspNetCore` (MVC-specific, deprecated); validators are
  invoked directly in endpoint filters instead.

## Rationale

No alternative was evaluated for the EF Core provider: `Npgsql.EntityFrameworkCore.PostgreSQL`
is the de facto standard and the only one with first-class PostgreSQL support
(arrays, `DateOnly`/`timestamptz` mapping, raw SQL). For validation, the
alternative is hand-rolled validation in endpoint bodies, which FluentValidation's
declarative rule syntax and testability beat for anything beyond a null check.

## Consequences

None beyond what ADR 0002 (no repository abstraction) and ADR 0003
(Testcontainers) already establish — `AppDbContext` is the abstraction,
migrations and query classes live under `api/Api/Features/<Feature>/`.
