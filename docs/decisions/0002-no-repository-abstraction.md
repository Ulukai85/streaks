# ADR 0002 — No repository abstraction over EF Core

Date: 2026-07-24
Status: Accepted

## Context

Prior projects used controller → service → repository. Most C# examples and a large
share of LLM-generated code assume the same. The question is whether this project
should follow suit.

EF Core's `DbContext` already implements Unit of Work; `DbSet<T>` already implements
Repository. A hand-written repository layer is therefore an abstraction over an
abstraction, unless it earns its place some other way.

## Decision

No repository interfaces. Endpoints and services use `AppDbContext` directly.
Non-trivial reads are encapsulated in named query classes (e.g. `StreakQuery`) with
no interface and no registration ceremony.

## Rationale

- **Testability, the usual argument, does not apply here.** Tests run against real
  PostgreSQL via Testcontainers (ADR 0003). A mocked repository would verify that our
  code calls a method we wrote — not that the query is correct, that the index is used,
  or that the unique constraint fires. The integration test is strictly stronger and
  removes the primary motivation for the interface.
- **Provider independence is not a real requirement.** This project will not swap
  PostgreSQL. If it did, the abstraction would leak anyway: `IQueryable` semantics,
  transaction scope, and change tracking do not survive the swap.
- **Query encapsulation is a real need** and is met by query classes, which give the
  same benefit at a fraction of the surface area.

## Consequences

- Fewer files; endpoint-to-SQL is one hop and readable.
- `DbContext` appears in endpoint signatures. Accepted.
- Unit-testing business logic in isolation requires either a pure function
  (`PeriodCalculator`) or an integration test. This is a deliberate forcing function
  toward pure domain logic.
- Reversing this later means touching every read path. Accepted as the cost of the
  simpler design.

## Alternatives considered

- **Generic `IRepository<T>`** — rejected: leaks `IQueryable` and provides no
  meaningful abstraction.
- **Per-aggregate repositories** — rejected: mostly pass-through methods; the genuinely
  complex queries are better served by named query classes.
