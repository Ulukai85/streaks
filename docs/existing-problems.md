# Existing Problems

Known issues we've deliberately left unresolved, with the reasoning, so they
don't get re-investigated from scratch or mistaken for new regressions.

## `npm audit` reports 17 vulnerabilities in `web/` (6 moderate, 11 high)

**Symptom:** `npm audit` in `web/` reports vulnerabilities in `axios`,
`brace-expansion`, and `@hono/node-server`.

**Cause:** `@spartan-ng/cli` (see ADR 0006) supports both Angular CLI and Nx
workspaces, so it depends on the entire Nx toolchain (`nx`, `@nx/workspace`,
`@nx/js`, `@nx/angular`, etc.) even though this repo is a plain Angular CLI
workspace and never exercises Nx code paths. `nx@23.1.0` — the current
latest stable release — hard-pins `axios@1.16.1` and
`brace-expansion@5.0.6` as exact versions, both in vulnerable ranges. The
`@hono/node-server` advisory comes from a separate chain: `@angular/cli`
bundles an MCP server that depends on `@modelcontextprotocol/sdk`, which
depends on the vulnerable Hono adapter.

**Why left as-is:** all of this sits in `devDependencies`, only reachable
through Nx-specific code paths this repo never invokes — none of it reaches
the built app. `npm audit fix` cannot resolve it: there is no newer nx
release yet with patched pins, so nothing satisfies the constraint without
forcing. `npm audit fix --force` would downgrade `@angular/cli` to 21.0.4 to
resolve the Hono chain, which is a larger regression than the vulnerability
it fixes.

**Revisit when:** Nx or spartan-ng cut a release with updated `axios`/
`brace-expansion` pins (re-run `npm audit` in `web/` periodically), or if we
start actually using Nx-specific tooling, at which point the risk
calculation changes.

**Rejected fix:** adding an `overrides` entry in `web/package.json` to force
patched `axios`/`brace-expansion` versions was considered — it would
override an exact pin `nx` explicitly declared, with unclear compatibility
consequences for a package we don't meaningfully exercise. Not worth it for
a dev-only, unreachable vulnerability.
