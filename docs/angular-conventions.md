# Angular Conventions

Lighter-weight than an ADR: still deliberate choices, but ones that are
cheap to reverse and visible directly from the file tree, so they don't get
the permanence of `docs/decisions/`. See `docs/decisions/` instead for
choices that are both contested and expensive to reverse.

## spartan-ng components live in `src/app/ui/`, excluded from our lint rules

`ng g @spartan-ng/cli:ui` copies spartan-ng's "Helm" (styled) components into
`web/src/app/ui/<component>/` (configured in `web/components.json`,
`componentsPath: "src/app/ui"`) — see ADR 0006. This vendored code uses
spartan's own naming (`hlmCard`, `hlmBtn`, ...), not our `streaks` selector
prefix, and isn't reformatted to match. `web/eslint.config.js` excludes
`src/app/ui/**` globally so the project's `@angular-eslint/directive-selector`
/`component-selector` rules (and other style rules) don't fight the copied
code — don't rename these selectors or remove that exclusion; re-running the
CLI or its `healthcheck` generator expects the vendored code as-shipped.

Theme style is `vega` (`web/components.json`); import alias is
`@spartan-ng/helm` (e.g. `import { HlmCardImports } from '@spartan-ng/helm/card'`).
