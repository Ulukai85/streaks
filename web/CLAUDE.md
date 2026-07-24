# CLAUDE.md — web

Frontend-specific rules. See root `CLAUDE.md` and `docs/` for project-wide
context.

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
- UI text is in German; code, comments, and docs are in English. No
  localization/i18n framework in v1.
- Errors from the API arrive as RFC 7807 problem details — handle them in
  that shape, not as ad hoc error objects.
- Component/directive selector prefix is `streaks` (set in `angular.json`
  and enforced by `eslint.config.js`), not the CLI default `app`.

## Tooling

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
