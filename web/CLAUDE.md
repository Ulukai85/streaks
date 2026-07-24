# CLAUDE.md — web

Frontend-specific rules. See root `CLAUDE.md` and `docs/` for project-wide
context; `docs/angular-conventions.md` covers frontend code organization
conventions.

## Hard rules

- **Standalone components only. No `NgModule`.**
- **Signals for state**, held in a plain service — no NgRx or other
  external state library.
- Use `@if` / `@for` control-flow syntax, not `*ngIf` / `*ngFor`.
- Use `httpResource` for data fetching.
- **No new npm dependency without an ADR** in `docs/decisions/` first
  (three sentences: what problem, what alternative was considered, why
  this).

## Conventions

- Angular 22.
- **Tailwind CSS v4** for styling — CSS-first config (no `tailwind.config.js`),
  PostCSS plugin configured in `.postcssrc.json`. **spartan-ng** for UI
  components: `@spartan-ng/brain` (headless primitives) is an npm
  dependency, but Helm (styled) components are copied into the repo by
  `ng g @spartan-ng/cli:ui <name>` and owned/customized here, not pulled in
  as an opaque library. See ADR 0006.
- The spartan-ng agent skill is installed (`.claude/skills/spartan`, a
  symlink into `.agents/skills/spartan`) — it documents how to add/compose
  spartan components correctly; consult it instead of guessing at spartan
  APIs. Only the skill is used, not `@spartan-ng/mcp`.
- UI text is in German; code, comments, and docs are in English. No
  localization/i18n framework in v1.
- Errors from the API arrive as RFC 7807 problem details — handle them in
  that shape, not as ad hoc error objects.
- Component/directive selector prefix is `streaks` (set in `angular.json`
  and enforced by `eslint.config.js`), not the CLI default `app`.

## Tooling

- Use the **Angular CLI MCP server**, not context7, for Angular-specific
  questions (docs, best practices, code examples, migrations). context7
  stays the default for every other library/framework.
- Angular CLI is a local `devDependency`, not installed globally — use
  `npx ng ...` or the `npm run` scripts (`start`, `build`, `test`, `lint`,
  `format`, `format:check`), never a global `ng`.
- Node **24.15.0+** is required (Angular 22's minimum); pinned in
  `.nvmrc` (currently `24.18.0`) — run `nvm use` before working here.
- ESLint (`eslint.config.js`, flat config via `angular-eslint`) and
  Prettier (`.prettierrc`, ships with the Angular CLI scaffold) are both
  set up; `eslint-config-prettier` is layered in last so formatting and
  linting don't fight each other.

## Architectural non-goals

Do not introduce state-management libraries, GraphQL clients, or
microfrontend/module-federation setups. See PROJECT-BRIEF.md §3 for the
full list and rationale.
