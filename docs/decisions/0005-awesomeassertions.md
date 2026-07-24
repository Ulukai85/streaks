# ADR 0005 — AwesomeAssertions for test assertions

Date: 2026-07-24
Status: Accepted

## Context

`PROJECT-BRIEF.md` §4 locks xUnit + Testcontainers for testing but says
nothing about an assertion library — xUnit's own `Assert.*` is usable on its
own. FluentAssertions was the default reach for fluent, readable assertions,
but v8 (Jan 2025) moved to a commercial license via Xceed: free for
non-commercial/OSS use, otherwise a paid per-developer license. FluentAssertions
7.x stays Apache 2.0 but only receives critical bug fixes, no new features.

## Decision

Use `AwesomeAssertions` — a community fork of FluentAssertions v7, MIT-licensed,
actively maintained, API-compatible with the FluentAssertions syntax.

## Rationale

- **No licensing risk.** MIT, no revenue thresholds, no per-seat cost, nothing
  to revisit if this project's scope or usage ever changes.
- **Familiar API.** Same fluent `.Should()` syntax as FluentAssertions; no
  learning curve, and low-cost to move to plain FluentAssertions later if the
  fork is ever abandoned.
- Rejected **FluentAssertions**: the free tier likely covers a personal
  project today, but ties a future decision (would this ever be used
  commercially?) to a dependency choice made now.
- Rejected **Shouldly**: also free and maintained, but a different assertion
  syntax with no offsetting benefit over AwesomeAssertions here.
- Rejected **xUnit's built-in `Assert`**: no dependency at all, but noticeably
  less readable failure output and more verbose multi-property assertions,
  which matters for a project where integration tests (ADR 0003) are the
  primary safety net.

## Consequences

- Test code uses `result.Should().Be(...)`-style assertions throughout
  `api/tests/Api.Tests/`.
- If AwesomeAssertions stalls or FluentAssertions relicenses again, revisiting
  this is a mechanical `using` + package swap, not a rewrite.
