# Phase 3 — Authentication

> Working plan for Phase 3 (see `PROJECT-BRIEF.md` §11). Tracks stage status
> as work lands.

## Context

Phase 2 (challenges, completions, `PeriodStartFor`, streaks) is done — all of
PROJECT-BRIEF §1's v1 scope works end-to-end without auth. Per
`PROJECT-BRIEF.md` §11, Phase 3 is: *"Auth: ASP.NET Core Identity bearer
tokens, Angular interceptor, refresh flow, route guard. ADR on token storage
before coding."*

Today there is no auth at all — `DevCurrentUserProvider` hardcodes a single
dev user (`Guid.Parse("11111111-...")`), and every endpoint is open. `User :
IdentityUser<Guid>` already exists (ADR 0007), but only as an entity shape;
no `AddIdentity()`, no auth middleware, no login flow. This phase makes that
real: a login/refresh/logout API, protecting the existing CRUD endpoints, and
an Angular-side interceptor + guard that keep a session alive across page
reloads without ever putting the refresh token somewhere JavaScript can read
it.

**Confirmed decisions (do not re-litigate):**

1. **Token storage**: refresh token in an httpOnly cookie set by the backend;
   access token held only in an in-memory Angular signal (never persisted).
   Formalized as ADR 0008.
2. **Endpoints**: hand-rolled `Features/Auth/` slice (matches
   Challenges/Completions/Dashboard conventions), not the built-in
   `MapIdentityApi<User>()`.
3. **Registration**: out of scope. Single account, created via seeding — no
   public `/register` endpoint, no registration form.
4. **Refresh trigger**: reactive — interceptor refreshes once on a 401 and
   retries, no proactive timer.
5. **Deploy topology**: same-origin in production — Caddy serves the built
   Angular app and proxies `/api/*` to the backend under one domain. This is
   what makes `SameSite=Strict` on the refresh cookie safe; Phase 4 has to
   deliver this shape.
6. **Production account creation**: extend the existing seed to run in every
   environment (not just Development) whenever `Users` is empty, sourcing
   credentials from env vars in Production and falling back to hardcoded dev
   values only in Development. No manual one-off DB step.
7. **Logout UI**: build a minimal nav shell (small top bar in
   `app.html`/`app.ts`) with a logout button shown only when authenticated —
   there is currently no persistent chrome anywhere to hang one off.
8. **Login identifier**: username, not email.
9. **Refresh-token mechanism**: a hand-rolled `RefreshTokens` table (hash the
   token, store `ExpiresAt`/`RevokedAt`/a rotation family id), not Identity's
   internal `RefreshTokenProtector`. Chosen over the self-contained-ticket
   approach because it gives per-device logout (revoke one row, not every
   session everywhere) and reuse/replay detection (a rotated-away token being
   presented again revokes its whole family) — real properties for an app
   used from both a phone and a laptop, and no more code than getting the
   internal-protector repurposing right. The **access token** still goes
   through Identity's built-in `BearerTokenOptions.BearerTokenProtector` /
   `AddBearerToken` handler — that's the framework's actual intended usage
   (validating a short-lived `Authorization` header on every request), and
   the revocation-granularity concern doesn't meaningfully apply to a
   15-minute token.
10. **Lockout policy**: set explicitly via `AddIdentityCore` options
    (`MaxFailedAccessAttempts`, `DefaultLockoutTimeSpan`) rather than left as
    an unreviewed framework default — a security-relevant number should be a
    deliberate choice.
11. **Test-suite hashing cost**: lower the password-hasher iteration count
    specifically under the Testing environment (`ApiFactory`'s test host
    config), so `IntegrationTestBase` performing a real `/login` once per
    test method doesn't add meaningful PBKDF2 latency to the suite as it
    grows. Production hashing strength is untouched.
12. **Production seed fails loudly**: if `!IsDevelopment()` and
    `SEED_USER_PASSWORD` is unset, throw at startup and refuse to boot —
    never silently fall back to the hardcoded dev password outside
    Development.

**Ordering:** backend fully first, then frontend fully — no interleaving.
Each stage below is one reviewable diff; stop after each for review.

---

## Stage 1 — ADRs, new package, EF/Identity wiring (no endpoints yet)

**Status: Done.** ADR 0008 (token storage) and ADR 0009 (Identity EF store)
formalize decisions #1 and #9. Added `Microsoft.AspNetCore.Identity.EntityFrameworkCore`,
`Api/Domain/RefreshToken.cs` + its EF config, `AddIdentityCore<User>()`/
`AddAuthentication`/`AddBearerToken` wiring in `Program.cs`, and migration
`20260728175809_AddIdentityStores`. See ADR 0009 and `api/CLAUDE.md`'s hard
rules for the durable design facts (why `AppDbContext` isn't
`IdentityDbContext`, why refresh tokens are hand-rolled). Verified: clean
build, migration applies with no pending model changes, full test suite
green (nothing yet depended on auth).

---

## Stage 2 — `Features/Auth/` slice (login, refresh, logout)

**Status: Done.** `RefreshTokenIssuer` (issue/rotate/revoke, hand-rolled
per decision #9) and `AuthEndpoints` (`POST /api/auth/{login,refresh,logout}`)
landed, plus `LoginRequest`/`LoginRequestValidator`/`LoginResponse`. The
access-token minting mechanics (matching `BearerTokenHandler.CreateBearerTicket`
by hand rather than going through `Context.SignInAsync`) are documented in
`api/CLAUDE.md`'s hard rules — that's the fact worth remembering here.
Verified: 9 new tests covering login success/failure, rotation, reuse
detection revoking a whole token family, expiry, and logout; full suite
138/138; manual `curl` smoke pass confirmed the cookie shape ADR 0008
specifies.

---

## Stage 3 — Protect existing endpoints, replace `DevCurrentUserProvider`, real seed

**Status: Done.** `HttpCurrentUserProvider` (reads `ClaimTypes.NameIdentifier`)
replaced `DevCurrentUserProvider`; Challenge/Completion/Dashboard endpoint
groups gained `.RequireAuthorization()` (`HealthEndpoints` stays open for
Compose/Caddy health checks). `DatabaseInitializer`/`DevSeed` now seed a real
`UserManager`-backed account in every environment, sourcing credentials from
`SEED_USER_NAME`/`SEED_USER_PASSWORD` with a Development-only hardcoded
fallback and a hard startup throw otherwise (decision #12) — the
credential-resolution logic is a separate `DatabaseInitializer.ResolveSeedCredentials`
method specifically so `DatabaseInitializerTests` can unit-test the throw
without a database. `IntegrationTestBase` now logs in for real against the
seeded user before every test; `ApiFactory` deliberately stays on
`UseEnvironment("Development")` rather than `"Testing"` — see
`docs/troubleshooting.md`'s `WebApplicationFactory`/`TestServer` cookie
gotchas section for why. Verified: full suite 142/142, every pre-existing
Challenges/Completions/Dashboard test now passing through a real login;
manual `curl` pass confirmed `/api/health` stays open, `/api/challenges/`
401s anonymously and 200s with a real token.

---

## Stage 4 — Angular: `AuthService`

**Status: Done.** `web/src/app/features/auth/auth.model.ts` (`LoginRequest`/
`LoginResponse`, `expiresAt` typed as `string` per the existing date-field
convention) and `auth.service.ts` landed — `@Service()`, an in-memory
`signal<string | null>` access token exposed as `token` (readonly, for the
Stage 5 interceptor) and `isAuthenticated = computed(...)`.
`login()`/`refresh()`/`logout()` call `/api/auth/{login,refresh,logout}` via
`HttpClient` + `firstValueFrom`, each with `withCredentials: true`.
`refresh()` failure clears the token and rethrows; `logout()` clears the
token in a `finally` regardless of response outcome. No `httpResource` is
used (no read state, only mutations), so `TestBed.tick()` wasn't needed in
`auth.service.spec.ts` — plain `HttpTestingController` request/flush per
call, matching `challenges.service.spec.ts`'s non-resource assertions.
Verified: `ng test` 19/19 green (5 new), `ng lint` clean.

---

## Stage 5 — Angular: `authInterceptor` + `authGuard`

**Status: Done.** `auth.interceptor.ts` attaches `Authorization: Bearer
<token>` to same-origin API requests (`req.url.startsWith(environment.apiUrl)`)
when `AuthService.token()` is set; on a 401 whose request URL isn't itself
under `${environment.apiUrl}/auth/`, it calls `authService.refresh()` once
(via `from`/`switchMap`, the RxJS exception to the house `firstValueFrom`
style since interceptors must return an `Observable`) and retries the
original request once with the new token — on refresh failure it propagates
the *original* 401 rather than the refresh's own error, relying on
`AuthService.refresh()` already clearing the token signal (Stage 4) so the
next guarded navigation naturally redirects. `auth.guard.ts` is a functional
`CanActivateFn`: allows if already authenticated, otherwise attempts a
silent `refresh()` (covers hard-reload landing on a guarded route) and
returns a `UrlTree` to `/login` on failure — the single place silent-refresh
happens. `app.config.ts` wires `provideHttpClient(withInterceptors([authInterceptor]))`;
`app.routes.ts` adds `canActivate: [authGuard]` to the Dashboard (`''`) and
Challenges routes (`health` stays open). Tests use
`TestBed.runInInjectionContext` to invoke the guard/interceptor directly,
with `HttpTestingController` driving `AuthService`'s real HTTP calls and a
hand-rolled `HttpHandlerFn` test double (`vi.fn<HttpHandlerFn>(...)`) as
`next` for the interceptor cases (token attached/omitted, 401-then-retry,
no-retry-on-auth-endpoint-401, refresh-failure propagates original error).
Verified: `ng test` 27/27 green (5 guard + 5 interceptor new), `ng lint`
clean, `ng build` clean.

---

## Stage 6 — Angular: Login UI + nav shell

**Status: Done.** `login-page.ts` (+ `.spec.ts`) follows `challenge-form.ts`'s
Signal Forms/Helm-input/problem-details-error pattern (`required()` on
`username`/`password`, mirroring the backend's `NotEmpty`-only validator),
wrapped in a centered `hlmCard` like `health-status.ts`. On success it calls
`authService.login()` then `router.navigateByUrl('/')`; on a 401 it reuses
`challenge-form.ts`'s exact non-field `{ kind: 'server', message:
error.detail ?? error.title }` mapping — no `errors` map exists on the
login 401, so no per-field mapping was needed. `app.routes.ts` adds an
unguarded `{ path: 'login', component: LoginPage }`. Per decision #7,
`app.ts`/`app.html` now render a minimal top bar with an "Abmelden" button
(`hlmBtn`) shown only when `authService.isAuthenticated()`, wired to
`logout()` → `navigateByUrl('/login')`.

One non-obvious test gotcha hit and fixed: `fixture.whenStable()` doesn't
wait for a plain-`async`-function submission action the way it does for a
router navigation or an `httpResource` — Angular's router integrates with
the framework's pending-task tracking (so the success-path test's single
`whenStable()` happened to work), but a bare `await authService.login()`
promise chain inside a Signal Forms submission action isn't tracked at all,
so `whenStable()` can resolve before the action (and thus the rendered
error) has actually settled. `login-page.spec.ts`'s failure-path test uses
an explicit microtask-draining helper instead of `whenStable()` to wait for
this deterministically — a pattern worth reusing for any future test that
asserts on a Signal Forms submission error rendered from an `HttpClient`
call.

Verified: `ng test` 32/32, `ng lint`/`ng build` clean. Manual browser pass
(via a headless-Chromium Playwright driver script, backend run locally with
`dotnet run` against the Compose Postgres, frontend via `ng serve`):
hitting a guarded route while logged out redirects to `/login`; wrong
credentials show the generic German error and don't navigate; correct
credentials (`dev`/`Dev-Password-123!`, the Development seed) land on the
dashboard with "Abmelden" visible; a hard reload stays on the dashboard
(silent refresh via the cookie through `authGuard`); `/challenges` is
reachable; logging out redirects to `/login` and a subsequent guarded-route
hit bounces back to `/login`; `/health` renders with no session throughout.

---

## Stage 7 — Phase 3 close-out verification

**Status: Not started**

- `dotnet test` and `ng test` both green, full suites.
- Re-read `docs/decisions/0008-token-storage.md` and
  `0009-identity-ef-store.md` against the existing ADRs' three-sentence
  format.
- Manual end-to-end walkthrough by the human: fresh browser session → login
  page → log in → dashboard/challenges reachable → hard reload stays logged
  in → logout → guarded routes redirect to login → `/health` still open.
- Explicitly out of scope and untouched this phase: registration UI,
  Compose/Caddy/CI (Phase 4), heatmap/OTel/PWA/backups (Phase 5).

---

## Critical files for the remaining (frontend) stages

- `web/src/app/app.config.ts`, `web/src/app/app.routes.ts` — interceptor/
  guard/route wiring (Stage 5).
- `web/src/app/features/challenges/challenge-form.ts` — Signal Forms +
  problem-details pattern to replicate for the login form (Stage 6).
- Backend endpoints to build against: `POST /api/auth/{login,refresh,logout}`
  (`api/Api/Features/Auth/AuthEndpoints.cs`), `LoginRequest`/`LoginResponse`
  shapes in the same folder.

## Process notes for execution

- One scoped stage per turn, per project convention — do not batch multiple
  stages into a single implementation turn.
