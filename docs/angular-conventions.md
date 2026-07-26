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

## Wrapping a spartan/brain control for Signal Forms: don't use `<ng-content>`

Signal Forms' `[formField]` directive binds natively to `<input>`/`<select>`/
`<textarea>` and to any component implementing `FormValueControl<T>` or
`FormCheckboxControl` (`@angular/forms/signals`) — a `model()` signal, not
the classic `ControlValueAccessor`. Several spartan/brain components (e.g.
`hlm-toggle-group`/`BrnToggleGroup`) still implement CVA, so `[formField]`
can't bind to them directly; the fix is a small local wrapper component that
implements `FormValueControl<T>` and forwards to the spartan component via
its plain `value`/`valueChange` inputs/outputs (most Brain components expose
these independently of the CVA path — confirmed for `BrnToggleGroup`).

The gotcha: **do not build that wrapper by projecting the spartan
component's item markup through `<ng-content>`.** `BrnToggleGroupItem`
resolves its parent via a DI token (`injectHlmToggleGroup()` →
`inject(HlmToggleGroupToken)`), and content projected through `<ng-content>`
keeps the *declaring* template's injector context, not the location it
renders into — so a `<button hlmToggleGroupItem>` written in the *consuming*
component's template and projected into the wrapper's internal
`<hlm-toggle-group>` throws `NG0201: No provider found for
InjectionToken HlmToggleGroupToken` at runtime, in the browser and in tests
alike. `ng build`/`ng lint` do not catch this (an unrecognized attribute
directive on a native element like `<button>` is silently inert, not a
compile error, if the directive also isn't imported — which is exactly how
this shipped once before being caught: the missing import masked the DI
bug by preventing the directive from ever activating). The fix is to render
the item markup (buttons, in the toggle-group case) *inside the wrapper's
own template*, driven by a data `@Input()`/`input()` (e.g. an
`options: {value, label}[]` array with a `@for`), not projected from the
consumer.

Same root cause applies to any Brain component whose child directives use
`injectXyz()`-style DI tokens to find a parent — check for that pattern
before reaching for `<ng-content>` in a Signal Forms wrapper.

## Test real interaction for custom form controls, not just model state

A test that only calls `formModel.set(...)` and asserts on the resulting
field state can pass even when the actual rendered control is broken (see
the `NG0201` gotcha above — it shipped past `ng test` once because the
tests never simulated a real click on the toggle-group buttons). For a
custom `FormValueControl` wrapper, include at least one test that drives it
through the DOM (`.click()`/dispatched events on the actual projected/rendered
elements) rather than only through the underlying signal.
