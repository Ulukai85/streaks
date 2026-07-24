# CLAUDE.md — api

Backend-specific rules. See root `CLAUDE.md` and `docs/` for project-wide
context; `docs/domain.md` is authoritative for period/streak logic, and
`docs/aspnet-conventions.md` covers code organization (vertical slices, feature
folders, when a service class is warranted).

## Hard rules

- **No `DateTime`.** Use `DateTimeOffset` for instants and `DateOnly` for
  calendar dates only. This is banned, not discouraged.
- **`<Nullable>enable</Nullable>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`**
  project-wide. Nullable warnings are build errors, not suggestions.
- **`AsNoTracking()` on every read path.** No exceptions in v1.
- **No repository pattern over EF Core.** `DbContext` is already the
  abstraction (see ADR 0002).
- **No new NuGet dependency without an ADR** in `docs/decisions/` first
  (three sentences: what problem, what alternative was considered, why
  this).
- Integration tests use **Testcontainers with real PostgreSQL** — never
  in-memory, never SQLite (see ADR 0003). If `dotnet test` fails pulling an
  image with a Docker "Unauthorized" error, see `docs/troubleshooting.md` —
  it's a local Docker config issue, not a code problem.
- Test assertions use **AwesomeAssertions**, not FluentAssertions (license
  change in FA v8 — see ADR 0005).
- `Cadence` is persisted **as a string**, not an int.
- Write the streak query as **raw SQL** via `db.Database.SqlQuery<T>()`.
  Do not attempt gaps-and-islands in LINQ.

## Conventions

See `docs/aspnet-conventions.md` for code organization. Beyond that:

- FluentValidation for request validation.
- Errors returned as RFC 7807 problem details.
- Sensitive-data logging enabled in Development; generated SQL is expected
  to be read during development.
- Code and comments in English.

## Off-limits without asking first

`PeriodStartFor` and the streak SQL are written by the human, by hand (see
`docs/domain.md` and PROJECT-BRIEF.md §10). If a task seems to require
touching either, stop and confirm before writing code.

## Architectural non-goals

Do not introduce MediatR/CQRS/event sourcing, Clean Architecture beyond
`Api` + `Api.Tests`, microservices, message queues, or GraphQL. See
PROJECT-BRIEF.md §3 for the full list and rationale.
