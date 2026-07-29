# ASP.NET Conventions

Lighter-weight than an ADR: still deliberate choices, but ones that are
cheap to reverse and visible directly from the file tree, so they don't get
the permanence of `docs/decisions/`. See `docs/decisions/` instead for
choices that are both contested and expensive to reverse (e.g. ADR 0002, no
repository abstraction).

## Vertical slices, not horizontal layers

Code is organized by feature, not by technical layer. Each feature folder
under `api/Api/Features/<Feature>/` owns its own endpoints, DTOs, and
validators — there is no top-level `Controllers/`, `Services/`, or `Dtos/`
grouping everything by kind instead of by feature.

Mildly contested — plenty of C# codebases default to horizontal layering —
but reversing it later is just a folder move, and the current structure is
already visible by looking at the tree. That combination is what keeps this
a convention rather than an ADR.

## Write a service class only when it holds a rule

Don't add a service class whose methods only forward to `AppDbContext` —
that's a pass-through wrapper, and `AppDbContext` is already the
abstraction (see ADR 0002). Write a service class when it actually
encapsulates a business rule (validation that spans multiple entities,
an operation with real side effects to sequence). For a non-trivial read
with no rule to enforce, write a named query class instead (e.g.
`StreakQuery`), not a service.

This is a heuristic, not a hard boundary: if in doubt, ask whether the
class would still exist if you deleted the one rule it enforces. If not,
it's a pass-through and should go.

## Minimal APIs over controllers

Already locked in `PROJECT-BRIEF.md` §4's stack table — Minimal APIs,
grouped by feature. Not reconsidered here; no separate convention or ADR
needed beyond that line.

## Startup-phase logic as extension methods, not inline in `Program.cs`

Cross-cutting startup work (migrating the database, seeding dev data) lives
as a static extension method under `Api/Data/` — e.g.
`DatabaseInitializer.MigrateAndSeedAsync(this WebApplication app)` — called
as a single line from `Program.cs`, the same way endpoint registration is
`app.MapHealthEndpoints()` rather than inline route declarations. Keeps
`Program.cs` readable as a table of contents (registration phase / startup
phase / pipeline phase) instead of accreting logic as the app grows.
`Api/Data/` holds this kind of cross-cutting data infrastructure (also
`AppDbContext`, `Configurations/`, `DevSeed`) — it isn't a feature, so it
doesn't belong under `Features/`.

## Primary constructors for dependency-holding classes

Prefer a primary constructor (`class Foo(Dependency dep) : Base(dep)`) over a
manually written constructor + backing fields, for classes whose constructor
body would only assign parameters to fields — test fixtures, `WebApplicationFactory`
subclasses, base classes, anything DI-shaped. `AppDbContext(DbContextOptions<AppDbContext> options)`,
`ApiFactory(PostgresFixture postgres)`, and `IntegrationTestBase(PostgresFixture postgres, ApiFactory factory)`
are all this shape.

**Exception:** domain entities under `Api/Domain/` (`User`, `Challenge`,
`Completion`) keep object-initializer style with `required` settable
properties, not primary constructors — EF Core materializes and updates them
through property setters by convention, and `new Challenge { Name = ..., }`-style
construction (Phase 2 Stage 4's create endpoint, tests) reads better against
that than positional constructor args would. Reach for a primary constructor by
default; keep the object-initializer style for entities that EF owns.

## EF configuration habits for new entities

- Pin `DateOnly` columns with `.HasColumnType("date")` explicitly in the
  `IEntityTypeConfiguration<T>`, even where the Npgsql default mapping
  already produces `date` — don't rely on the provider default for a column
  type this load-bearing (see `Completion.PeriodStart`, `docs/domain.md`).
- Default new foreign keys to `.OnDelete(DeleteBehavior.Restrict)`. EF's
  cascade-by-default should be the exception, opted into deliberately, not
  the default outcome of an unconfigured `HasOne`/`HasForeignKey` — most
  entities here are archived rather than deleted, so a silent cascade is
  usually a bug, not a feature.
