# CLAUDE.md — web

Frontend-specific rules. See root `CLAUDE.md` and `docs/` for project-wide
context; `docs/angular-conventions.md` covers frontend code organization
conventions.

## Hard rules

- **Standalone components only. No `NgModule`.**
- **Signals for state**, held in a plain service — no NgRx or other
  external state library.
- Use `@if` / `@for` control-flow syntax, not `*ngIf` / `*ngFor`.
- Use `httpResource` for data fetching (reads only). For mutations
  (POST/PUT/DELETE), use `HttpClient` directly and bridge to a `Promise` via
  `firstValueFrom` for `async`/`await` call sites — this is Angular's own
  documented guidance ("avoid `httpResource` for mutations"), not a
  house-specific deviation. Don't hand-roll RxJS operator chains or manual
  `.subscribe()`; `firstValueFrom` is the one-shot-request idiom that pairs
  with signals-based state.
- Don't set `changeDetection: ChangeDetectionStrategy.OnPush` explicitly —
  it's the default in Angular v22+.
- Prefer `@Service()` over `@Injectable({providedIn: 'root'})` for new
  singleton services that only use `inject()` (no constructor DI, no
  `useClass`/`useFactory` provider config — reach for `@Injectable` if you
  need those).
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
- Prefer **Signal Forms** (`@angular/forms/signals`: `form()`, `FormField`,
  `FormRoot`, `submit()`) over Reactive Forms for new forms — matches
  Angular's current best-practices guidance for v22+. Reactive Forms remain
  an acceptable fallback when bridging a legacy/third-party
  `ControlValueAccessor` outweighs rewriting it (see `compatForm`/
  `SignalFormControl` in `@angular/forms/signals/compat` for that bridging).
  Custom form controls (e.g. wrapping a spartan/brain component) must
  implement `FormValueControl<T>`/`FormCheckboxControl`, not classic CVA —
  see `docs/angular-conventions.md` for a concrete gotcha around this.

## Tooling

- Use the **Angular CLI MCP server**, not context7, for Angular-specific
  questions (docs, best practices, code examples, migrations). context7
  stays the default for every other library/framework. Call
  `get_best_practices` (and `search_documentation` for specific APIs) while
  **planning** Angular work, before choosing an implementation approach —
  not only when asked or when stuck. An existing plan doc's wording (e.g. a
  stage spec written in an earlier session) is a starting point, not a
  substitute for checking current guidance; Angular's forms/DI/change-detection
  defaults have moved fast enough that a plan written a few stages ago can
  already be behind (this bit us once — Stage 8's form shipped as Reactive
  Forms + explicit `OnPush` + `@Injectable({providedIn:'root'})` before a
  follow-up pass caught all three against the current best-practices guide).
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
