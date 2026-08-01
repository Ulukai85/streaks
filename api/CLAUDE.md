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
- **No endpoint reads a hardcoded user id directly.** Inject
  `ICurrentUserProvider` (`Api/Infrastructure/`) instead — `HttpCurrentUserProvider`
  reads it from the authenticated request's `ClaimTypes.NameIdentifier` claim.
- **No repository pattern over EF Core.** `DbContext` is already the
  abstraction (see ADR 0002).
- **No new NuGet dependency without an ADR** in `docs/decisions/` first
  (three sentences: what problem, what alternative was considered, why
  this). `IdentityUser<TKey>` ships in the shared framework and needed no
  new package (ADR 0007); `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
  (the EF user store) is the one real package added so far (ADR 0009).
- **`AppDbContext` is a plain `DbContext`, not `IdentityDbContext`** — the
  three Identity entity tables are configured inline in `OnModelCreating`,
  and `UserConfiguration` pins the column config Identity would otherwise
  supply automatically. Full reasoning in ADR 0009.
- **Refresh tokens are hand-rolled** (`Api/Domain/RefreshToken.cs` +
  `RefreshTokens` table) for per-device logout and reuse detection, not
  Identity's `RefreshTokenProtector`. The **access** token does go through
  Identity's `BearerTokenOptions.BearerTokenProtector` via `AddBearerToken`
  — minted by hand-building an `AuthenticationTicket` with scheme
  `$"{IdentityConstants.BearerScheme}:AccessToken"` and passing it straight
  to `BearerTokenProtector.Protect(...)` (see
  `AuthEndpoints.MintAccessTokenAsync`), not via `Context.SignInAsync`,
  which would write the framework's own response body instead of ours. This
  mirrors `BearerTokenHandler.CreateBearerTicket` exactly (source:
  `dotnet/aspnetcore`) so the framework's own validation handler accepts it.
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
- **Two `db.Database.SqlQuery<T>()` gotchas** (hit and fixed while building
  `StreakQuery`, will bite any future raw-SQL query the same way): a
  trailing `;` inside the raw SQL text breaks composition when a LINQ
  operator like `.FirstOrDefaultAsync()` follows (EF wraps the raw SQL in a
  subquery, and the semicolon makes that invalid SQL); and result column
  aliases must match the target record's property names **exactly** — EF
  does not translate `snake_case`/lowercase column names to PascalCase
  properties without the (unused here) `EFCore.NamingConventions` package,
  so `AS ends_at` silently fails to bind to a property named `EndsAt`.

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
- **The container runs as non-root** (`USER $APP_UID`, the .NET runtime
  image's built-in user — see `api/Dockerfile`). If a new feature needs to
  write to disk (temp files, more persisted state beyond the Data
  Protection key ring), it needs its own directory created and `chown`ed to
  `$APP_UID` in the Dockerfile first — `/keys` is the existing pattern to
  copy. Writing to an arbitrary path will fail with
  `UnauthorizedAccessException` at runtime, not at build time.

## Architectural non-goals

Do not introduce MediatR/CQRS/event sourcing, Clean Architecture beyond
`Api` + `Api.Tests`, microservices, message queues, or GraphQL. See
PROJECT-BRIEF.md §3 for the full list and rationale.
