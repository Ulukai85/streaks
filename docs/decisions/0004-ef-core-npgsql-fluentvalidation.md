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

## Addendum — HasData rejected for dev-user seeding (2026-07-25)

The Phase 2 Stage 1 dev user is seeded via code at startup
(`if (!await db.Users.AnyAsync()) db.Users.Add(...)`, gated to
`Development`), not EF Core's `HasData`. `HasData` requires compile-time
constant seed values baked into the migration snapshot — awkward for a
`User` row that gains ASP.NET Core Identity fields in Phase 3 (see ADR
0007), and any later edit to seed data via `HasData` produces a phantom
migration diff even when nothing about the schema itself changed. This is
the same "tooling/process note on an already-decided ADR" pattern already
used for `Microsoft.EntityFrameworkCore.Design` in Phase 2 Stage 0.
