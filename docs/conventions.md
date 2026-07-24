# Conventions

Lighter-weight than an ADR: still deliberate choices, but ones that are
cheap to reverse and visible directly from the file tree, so they don't get
the permanence of `docs/decisions/`. See `docs/decisions/` instead for
choices that are both contested and expensive to reverse (e.g. ADR 0002, no
repository abstraction).

## Vertical slices, not horizontal layers

Code is organized by feature, not by technical layer. Each feature folder
under `api/src/Features/<Feature>/` owns its own endpoints, DTOs, and
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
