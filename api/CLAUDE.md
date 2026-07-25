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
  this). Note: `IdentityUser<TKey>` (`Microsoft.AspNetCore.Identity`) and the
  rest of ASP.NET Core Identity ship in the shared framework — a
  `Microsoft.NET.Sdk.Web` project like `Api.csproj` references it implicitly,
  so deriving `User : IdentityUser<Guid>` (ADR 0007) did not need a new
  package. Only `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (stores,
  `IdentityDbContext`) would count as new — not needed yet, Phase 3's call.
- Integration tests use **Testcontainers with real PostgreSQL** — never
  in-memory, never SQLite (see ADR 0003). If `dotnet test` fails pulling an
  image with a Docker "Unauthorized" error, see `docs/troubleshooting.md` —
  it's a local Docker config issue, not a code problem.
- Test assertions use **AwesomeAssertions**, not FluentAssertions (license
  change in FA v8 — see ADR 0005). Use it consistently — `result.Should()...`
  even for type checks (`.Should().BeOfType<T>()`, `.Which` to unwrap). Only
  fall back to xUnit's `Assert` when there's genuinely no AwesomeAssertions
  equivalent.
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

## Deployment

- DB connection string key is `ConnectionStrings:Postgres` —
  `appsettings.Development.json` for local dev, `ConnectionStrings__Postgres`
  env var in Docker Compose/Production (no `appsettings.Production.json`).
  See root `README.md` for the full run/deploy process.
- `Features/Health/` is a live example of the vertical-slice convention
  (`docs/aspnet-conventions.md`) — pattern-match new features against it.
- `api/.dockerignore` must keep excluding `bin/`/`obj/` — otherwise a local
  build's artifacts get copied into the Docker image and clobber the
  container's own `dotnet restore`.

## Off-limits without asking first

`PeriodStartFor` and the streak SQL are written by the human, by hand (see
`docs/domain.md` and PROJECT-BRIEF.md §10). If a task seems to require
touching either, stop and confirm before writing code.

## Architectural non-goals

Do not introduce MediatR/CQRS/event sourcing, Clean Architecture beyond
`Api` + `Api.Tests`, microservices, message queues, or GraphQL. See
PROJECT-BRIEF.md §3 for the full list and rationale.
