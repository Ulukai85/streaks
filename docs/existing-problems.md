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

**Dependabot confirmation (2026-08-02):** after enabling Dependabot, it
reached the same conclusion independently. It auto-opened a fix PR for
`axios`, but reported `@hono/node-server` and `brace-expansion` as
non-auto-fixable for the reasons already documented above (the only
available paths require downgrading `@angular/cli` from 22.0.8 to 21.0.4,
or updating `@spartan-ng/cli`'s pinned Nx/`minimatch` chain). Both alerts
were dismissed on GitHub as tolerable risk, pointing back to this entry.
Earliest fixed versions per Dependabot, for reference when revisiting:
`@hono/node-server@2.0.10`, `brace-expansion@2.1.3`.

## No `Content-Security-Policy` header (Stage 5, decision #10)

**Symptom:** `infrastructure/Caddyfile` sets `Strict-Transport-Security`,
`X-Content-Type-Options`, `Referrer-Policy`, and `X-Frame-Options`, but not
`Content-Security-Policy`.

**Cause:** the built `web/dist/streaks/browser/index.html` has an inline
`<style>` block and an inline `onload="this.media='all'"` attribute —
Angular's automatic critical-CSS inlining, verified by reading the actual
build output, not assumed. A CSP compatible with that output would need
`'unsafe-inline'` on `style-src` and `script-src`, which defeats most of
CSP's point for scripts — allowing inline script execution is exactly what
CSP exists to block, and matters here given ADR 0008's XSS threat model for
the in-memory access token.

**Why left as-is:** doing this properly means disabling Angular's
critical-CSS inlining first, so the build stops emitting inline
`<style>`/`onload`, and only then shipping a CSP without `'unsafe-inline'`.
That's a frontend build-config change (`web/angular.json`), not something
to bundle quietly into a Caddyfile-only pass.

**Revisit when:** someone's ready to set
`optimization.styles.inlineCritical: false` in `web/angular.json`, confirm
the app still renders correctly without the inlined critical CSS (slightly
slower first paint is the expected tradeoff), and then add a real
`Content-Security-Policy` header to `infrastructure/Caddyfile`.
