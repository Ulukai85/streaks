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

**Off-limits:** none this phase — `PeriodStartFor` and the streak SQL aren't
touched by auth work.

**Ordering:** backend fully first, then frontend fully — no interleaving.
Each stage below is one reviewable diff; stop after each for review.

---

## Stage 1 — ADRs, new package, EF/Identity wiring (no endpoints yet)

**Status: Done** (migration `20260728175809_AddIdentityStores`)

Deviations from the plan as written, all minor:

- `UserConfiguration` also pins `HasMaxLength(256)` on `UserName`/
  `NormalizedUserName`/`Email`/`NormalizedEmail` and marks `ConcurrencyStamp`
  as a concurrency token — the plan only mentioned the unique index, but
  since `AppDbContext` deliberately isn't an `IdentityDbContext`, nothing
  else would have applied Identity's own column configuration. Migration
  narrows those four columns from `text` to `varchar(256)`; harmless, they
  are all null today.
- The unique index on `NormalizedUserName` is unfiltered — Postgres treats
  NULLs as distinct in a unique index, so no `WHERE ... IS NOT NULL` is
  needed.
- The three Identity tables get FKs to `Users` with
  `DeleteBehavior.Cascade`, not the project's usual `Restrict` default —
  deliberate and commented: those rows are meaningless without their user
  and `UserManager.DeleteAsync` relies on the store cleaning them up.
  `RefreshTokens` keeps `Restrict` as planned.

- New `docs/decisions/0008-token-storage.md` — formalizes decision #1 above.
  Content: httpOnly, `Secure`, `SameSite=Strict` refresh cookie
  (`Path=/api/auth`, no explicit `Domain`); access token only in an in-memory
  signal, never in localStorage/sessionStorage. Rationale: XSS can't read the
  refresh token; costs one silent-refresh round trip on hard reload. States
  the same-origin assumption (decision #5) as a stated precondition.
- New `docs/decisions/0009-identity-ef-store.md` — the one genuinely new
  NuGet package this phase: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
  (needed for `UserManager<User>`/`SignInManager<User>` via
  `AddEntityFrameworkStores<AppDbContext>()`; `IdentityUser<Guid>` itself
  already ships in the shared framework per ADR 0007 — this is the store
  package, which api/CLAUDE.md already flagged as "Phase 3's call"). Records
  `AddIdentityCore<User>()` called **without** `.AddRoles<TRole>()`, so EF
  Core only needs `UserClaims`/`UserLogins`/`UserTokens` tables — no
  `Roles`/`UserRoles`/`RoleClaims`. None of those three tables are actually
  used by the login/refresh/logout flow in Stage 2; they exist only because
  `UserOnlyStore`'s constructor requires them to be part of the EF model.
- Modify `api/Api/Api.csproj` / `api/Directory.Packages.props` — add the
  package reference + a pinned version matching the existing EF Core 10.0.10
  family.
- Modify `api/Api/Data/AppDbContext.cs` — stays a plain `DbContext` (not
  `IdentityDbContext`/`IdentityUserContext`), to keep table naming/config
  under this project's explicit control rather than inheriting framework
  defaults. Add the three Identity entity configurations directly in
  `OnModelCreating` (not new `IEntityTypeConfiguration<T>` files — these are
  framework plumbing, not domain entities).
- Modify `api/Api/Data/Configurations/UserConfiguration.cs` — add a unique
  index on `NormalizedUserName` (needed for `UserManager` uniqueness checks
  to behave correctly).
- New `api/Api/Domain/RefreshToken.cs` — per decision #9, the hand-rolled
  refresh-token store: `Id (Guid), UserId (Guid), FamilyId (Guid), TokenHash
  (string), ExpiresAt (DateTimeOffset), CreatedAt (DateTimeOffset), RevokedAt
  (DateTimeOffset?)`. `FamilyId` equals `Id` on first issuance and is carried
  forward on every rotation — it's what lets a reuse-detection hit revoke an
  entire chain in one update, not just the one row presented.
- New `api/Api/Data/Configurations/RefreshTokenConfiguration.cs` — unique
  index on `TokenHash`, index on `(UserId, FamilyId)`, FK to `User` with
  `DeleteBehavior.Restrict` (matches the project's default-FK-behavior
  convention in `docs/aspnet-conventions.md`).
- Add `DbSet<RefreshToken> RefreshTokens` to `AppDbContext`.
- New migration (`dotnet ef migrations add AddIdentityStores`) — adds the
  three Identity tables, `RefreshTokens`, the new `NormalizedUserName` index,
  and starts populating the already-present-but-dormant Identity columns on
  `Users`.
- Modify `api/Api/Program.cs` (registration phase only — no endpoints yet, so
  this stage builds/migrates/tests standalone):
  ```csharp
  builder.Services.AddHttpContextAccessor();
  builder.Services.AddIdentityCore<User>(options =>
      {
          options.Lockout.MaxFailedAccessAttempts = 5;
          options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
      })
      .AddEntityFrameworkStores<AppDbContext>()
      .AddSignInManager();
  builder.Services.AddAuthentication(IdentityConstants.BearerScheme)
      .AddBearerToken(IdentityConstants.BearerScheme, options =>
      {
          options.BearerTokenExpiration = TimeSpan.FromMinutes(15);
      });
  builder.Services.AddAuthorization();
  ```
  Note there's no `RefreshTokenExpiration` here — per decision #9 the
  built-in refresh path isn't used at all; the hand-rolled table's own
  `ExpiresAt` (set at issuance in Stage 2, 30-day default) governs refresh
  lifetime instead. Plus `app.UseAuthentication(); app.UseAuthorization();`
  before the `Map*Endpoints()` calls. Leave `DevCurrentUserProvider`
  registered for now — swapped in Stage 3, so this stage stays independently
  buildable. Access-token lifetime (15 min), lockout values (5 attempts / 15
  min), and refresh lifetime (30 days, Stage 2) are starting guesses for a
  daily-use personal app — adjustable, not load-bearing.

**Verify:** `dotnet build` clean under `TreatWarningsAsErrors`; migration
applies cleanly against local Postgres with no pending model changes
(including the new `RefreshTokens` table); full existing test suite still
green (nothing yet depends on auth).

---

## Stage 2 — `Features/Auth/` slice (login, refresh, logout)

**Status: Done**

Deviations from the plan as written:

- The access token is minted by hand-building an `AuthenticationTicket` (via
  `SignInManager.CreateUserPrincipalAsync` + `BearerTokenOptions.BearerTokenProtector.Protect`)
  rather than through `Context.SignInAsync`/`TypedResults.SignIn` — deliberate,
  not a shortcut: `SignInAsync` routes through `BearerTokenHandler.HandleSignInAsync`,
  which writes the framework's own `AccessTokenResponse` body (including a
  `refresh_token` field) directly to the HTTP response, which is exactly the
  shape ADR 0008 rejects. The ticket is built to match
  `BearerTokenHandler.CreateBearerTicket` exactly (verified against
  `dotnet/aspnetcore`'s actual source, not from memory) — same
  `$"{scheme}:AccessToken"` ticket-scheme string, same `ExpiresUtc` property —
  so the framework's own `BearerTokenHandler.HandleAuthenticateAsync` can
  unprotect and validate what we mint here with no special-casing later.
- `AuthEndpointsTests` creates and tears down its own `User` rows via
  `UserManager<User>` (helper `CreateTestUserAsync`/`DeleteTestUserAsync`)
  rather than depending on the global seeded dev user having a password —
  that wiring is Stage 3's job (`DevSeed`/`DatabaseInitializer`,
  "production account creation"). Teardown deletes `RefreshTokens` before the
  `User` row (the FK is `Restrict`), since `PostgresFixture.ResetDatabaseAsync`
  asserts exactly one seeded user survives every reset.
- `IntegrationTestBase` gained one line — `protected ApiFactory Factory { get; }`
  — so tests can resolve `UserManager<User>` from the same DI container the
  HTTP pipeline runs against. Everything else in `IntegrationTestBase`/
  `ApiFactory` is untouched; the "every test logs in for real" rewiring is
  still Stage 3's.
- One test-writing gotcha worth flagging for later auth tests: `WebApplicationFactory`'s
  default client (`HandleCookies = true`) silently overwrites a manually-set
  `Cookie` request header with its own stored cookie. The reuse-detection test
  (replaying an already-rotated token) needed a second client built with
  `new WebApplicationFactoryClientOptions { HandleCookies = false }` to get a
  request that actually carries the stale cookie.

**Verify:** all done — `dotnet build` clean under `TreatWarningsAsErrors`;
`dotnet test --filter Auth` green (9 tests: login success/wrong-password/
unknown-username all generic 401, refresh rotates and revokes the prior row,
missing cookie, reuse of an already-rotated token 401s **and** revokes the
whole family — confirmed a second rotation-generation token also then fails,
expired row 401s, logout 204 + immediately-following refresh 401s, logout
with no cookie present still 204s); full suite 138/138. Manual `dotnet run` +
`curl` smoke pass (against a throwaway seeded user, removed afterward)
confirmed the `Set-Cookie` is `HttpOnly; SameSite=Strict; Path=/api/auth`,
the login/refresh response bodies contain only `accessToken`/`expiresAt`, and
logout's `Set-Cookie` expires the cookie (`Thu, 01 Jan 1970`).

Per decision #9: the refresh token is entirely hand-rolled (table-backed),
no spike on internal Identity protectors needed for it. The access token
still goes through `BearerTokenOptions.BearerTokenProtector` (public,
post-configured by `AddBearerToken` via `IDataProtectionProvider`) — this is
the framework's actual intended usage (minting something its own
`BearerTokenHandler` middleware validates automatically on every protected
request via `app.UseAuthentication()`), so treat it as a normal build step,
not a spike: write the login endpoint, then one integration test confirming
a minted access token is accepted by a `.RequireAuthorization()` endpoint
end-to-end, before moving on.

- New `api/Api/Features/Auth/RefreshTokenIssuer.cs` — small dedicated class
  (per `docs/aspnet-conventions.md`'s "service class only when it holds a
  rule" — token generation/hashing/rotation/family-revocation is a real
  rule, not a pass-through). Responsibilities:
  - `Issue(userId)` — `RandomNumberGenerator.GetBytes(32)` → base64url-encode
    for the raw token (goes in the cookie) → SHA-256 hash it for storage.
    Inserts a new `RefreshToken` row (`FamilyId = Id` on first issuance,
    `ExpiresAt = now + 30 days`).
  - `Rotate(presentedRawToken)` — hashes the presented token, looks up by
    `TokenHash`. Not found → `null` (caller returns 401). Found but
    `RevokedAt` already set → **reuse detected**: revoke every row sharing
    that `FamilyId` (`UPDATE ... WHERE FamilyId = @familyId AND RevokedAt IS
    NULL`), return `null` (caller returns 401 — this is the compromise
    signal decision #9 exists for). Found, valid, unexpired → revoke this
    row, issue a new one carrying the same `FamilyId` forward, return it.
  - `Revoke(presentedRawToken)` — hashes and revokes just that one row (and
    only that row/family, not the whole user — the per-device-logout
    property decision #9 exists for). No-op, not an error, if not found.
- `api/Api/Features/Auth/AuthEndpoints.cs` — `MapAuthEndpoints`, group
  `/api/auth`, **not** `.RequireAuthorization()` on the group (login/refresh
  must be reachable unauthenticated; logout only needs the refresh cookie,
  not a bearer header).
  - `POST /login` — `UserManager.FindByNameAsync` +
    `SignInManager.CheckPasswordSignInAsync(lockoutOnFailure: true)`. Failure
    → generic 401 (never reveal which field was wrong). Success →
    `SignInManager.CreateUserPrincipalAsync(user)` protected via
    `BearerTokenOptions.BearerTokenProtector` for the access token, plus
    `RefreshTokenIssuer.Issue(user.Id)` for the refresh token. Returns
    `LoginResponse(AccessToken, ExpiresAt)` in the body; refresh token only
    as an httpOnly `Set-Cookie` (`Secure` off only in Development, over
    plain local HTTP).
  - `POST /refresh` — reads the refresh cookie (not a request body),
    `RefreshTokenIssuer.Rotate(...)`. `null` → 401 (frontend treats this as
    "session over"). Otherwise mint a new access token the same way as
    login, overwrite the cookie with the rotated refresh token, return a new
    `LoginResponse`.
  - `POST /logout` — reads the refresh cookie, `RefreshTokenIssuer.Revoke(...)`,
    clears the cookie, `204` regardless of whether a matching row existed —
    logout never itself errors.
- `LoginRequest.cs` (`record LoginRequest(string Username, string Password)`),
  `LoginRequestValidator.cs` (`NotEmpty()` both, registered in Program.cs),
  `LoginResponse.cs` (`record LoginResponse(string AccessToken, DateTimeOffset
  ExpiresAt)` — deliberately no `RefreshToken` field, per ADR 0008).
- Modify `Program.cs` — register the validator, `app.MapAuthEndpoints();`.

**Verify:** `dotnet test --filter Auth` green — login success/wrong-password/
unknown-username (all generic 401), refresh success (cookie value rotates,
old `TokenHash` row now `RevokedAt`-set), missing cookie, presenting an
already-rotated (revoked) token (401 **and** confirm the whole family got
revoked — a second `/refresh` with the *previous* rotation's token also now
fails), expired row, logout 204 + that exact token immediately rejected by a
following `/refresh`. Manual `dotnet run` + `curl` smoke pass confirming the
`Set-Cookie` is actually `HttpOnly` and the body never contains the refresh
token.

---

## Stage 3 — Protect existing endpoints, replace `DevCurrentUserProvider`, real seed

**Status: Not started**

- New `api/Api/Infrastructure/HttpCurrentUserProvider.cs` — primary
  constructor, reads `ClaimTypes.NameIdentifier` off
  `IHttpContextAccessor.HttpContext.User`. `ICurrentUserProvider` itself is
  unchanged (`{ Guid UserId { get; } }`) — logout/refresh resolve their user
  from the refresh-cookie ticket directly, not through this interface, so it
  stays exactly as minimal as it is today.
- Modify `Program.cs` — swap the `ICurrentUserProvider` registration to
  `HttpCurrentUserProvider`. Delete `DevCurrentUserProvider.cs` — nothing
  references it once this lands, and its own doc comment already says Phase
  3 replaces it.
- Modify `ChallengeEndpoints.cs` / `CompletionEndpoints.cs` /
  `DashboardEndpoints.cs` — add `.RequireAuthorization()` to each group.
  **`HealthEndpoints.cs` stays unprotected** — it's hit unauthenticated by
  the Docker Compose healthcheck and Caddy's health probing.
- Modify `api/Api/Data/DevSeed.cs` — add a real username/password so login
  actually works; switch the seeding call to go through
  `UserManager.CreateAsync(user, password)` instead of a raw `db.Users.Add`
  (needed so `PasswordHash`/`SecurityStamp`/`NormalizedUserName` get
  populated — a raw insert leaves `PasswordHash` null and login would always
  fail).
- Modify `api/Api/Data/DatabaseInitializer.cs` — per decision #6, drop the
  `IsDevelopment()` gate on seeding; keep the existing `!await
  db.Users.AnyAsync()` idempotency guard so it's safe to run in every
  environment. Source username/password from `SEED_USER_NAME`/
  `SEED_USER_PASSWORD` configuration, falling back to the hardcoded dev
  values only when `IsDevelopment()`. Per decision #12: if `!IsDevelopment()`
  and `SEED_USER_PASSWORD` is unset/empty, **throw at startup** — a fresh
  Production deploy must refuse to boot rather than silently seed the
  hardcoded dev password onto a real system. New test:
  `DatabaseInitializer` throws when Production-like config is missing the
  env var (host builder configured with `IsDevelopment() == false` and no
  `SEED_USER_PASSWORD`).
- Modify `api/Api.Tests/Infrastructure/ApiFactory.cs` — per decision #11,
  configure a lower `PasswordHasherOptions.IterationCount` (or register a
  test-only `IPasswordHasher<User>`) in the test host specifically, so every
  `IntegrationTestBase` test performing a real `/login` doesn't pay
  production-strength PBKDF2 cost. Production hashing config is untouched.
- **Test impact — every existing Challenges/Completions/Dashboard test will
  401 once this lands.** Modify `IntegrationTestBase.cs`: after
  `postgres.ResetDatabaseAsync()` in `InitializeAsync()`, log in for real
  against `/api/auth/login` with the seeded dev credentials and set
  `Client.DefaultRequestHeaders.Authorization`. This exercises the real login
  endpoint on every test run rather than bypassing it. Add
  `CreateAnonymousClient()` for tests asserting 401-without-token behavior
  and for the auth endpoint tests themselves. Note in a comment: the seeded
  `Users` row is shared across the whole test run (not reset per test) — a
  test calling `/logout` or triggering reuse-detection revokes that user's
  refresh-token family, so auth tests must each mint their own token rather
  than relying on a shared/cached one (already true by construction today).
- New `api/Api.Tests/Features/Auth/AuthEndpointsTests.cs` (if not already
  fully covered in Stage 2) — add the protected-endpoint smoke test: 401 via
  `CreateAnonymousClient()` on `GET /api/challenges/`, 200 via the default
  now-authenticated `Client`.

**Verify:** `dotnet test` — full suite green again, including every
pre-existing Challenges/Completions/Dashboard test now passing through real
login rather than an implicit hardcoded user. Manual `dotnet run` + `curl`
confirming `/api/health` still works with zero credentials and
`/api/challenges/` returns 401 with none.

---

## Stage 4 — Angular: `AuthService`

**Status: Not started**

- `web/src/app/features/auth/auth.model.ts` — `LoginRequest`, `LoginResponse`
  matching the backend shape (check `challenge.model.ts` for how existing
  date fields are typed over JSON before assuming a type for `expiresAt`).
- `web/src/app/features/auth/auth.service.ts` — `@Service()`, an in-memory
  `signal<string | null>` for the access token, `isAuthenticated =
  computed(...)`. `login()`/`refresh()`/`logout()` call
  `/api/auth/{login,refresh,logout}` via `HttpClient` + `firstValueFrom`,
  each with `withCredentials: true` explicitly set (belt-and-suspenders for
  same-origin cookie attachment). `refresh()` failure and `logout()` both
  clear the signal.
- `auth.service.spec.ts` — `TestBed.tick()` pattern per
  `docs/angular-conventions.md`, `HttpTestingController` for the three calls.

**Verify:** `ng test` green for the new spec.

---

## Stage 5 — Angular: `authInterceptor` + `authGuard`

**Status: Not started**

- `web/src/app/features/auth/auth.interceptor.ts` — functional
  `HttpInterceptorFn`, attaches `Authorization: Bearer <token>` from
  `AuthService` when present. On a 401 (and only if the failing request
  wasn't itself `/auth/...`, to avoid a refresh-loop), calls `refresh()` once
  and retries the original request; no retry counter beyond that single
  attempt, per decision #4.
- Modify `app.config.ts` — `provideHttpClient(withInterceptors([authInterceptor]))`.
- `web/src/app/features/auth/auth.guard.ts` — functional `CanActivateFn`. If
  already authenticated, allow. Otherwise attempt a silent `refresh()` first
  (covers hard-reload landing on a guarded route) before redirecting to
  `/login`. This is the single place silent-refresh happens — no separate
  bootstrap/`APP_INITIALIZER`-equivalent needed, since the only unguarded
  route is `/login` itself, which doesn't need auth state.
- Modify `app.routes.ts` — `canActivate: [authGuard]` on the Dashboard and
  Challenges routes.
- `auth.interceptor.spec.ts` / `auth.guard.spec.ts` — using
  `TestBed.runInInjectionContext` for the functional guard/interceptor
  testing pattern.

**Verify:** `ng test` green for both new specs; `ng lint`/`ng build` clean.

---

## Stage 6 — Angular: Login UI + nav shell

**Status: Not started**

- `web/src/app/features/auth/login-page.ts` (+ `.spec.ts`) — Signal Forms,
  spartan-ng Helm inputs, matching `challenge-form.ts`'s existing
  form/validation/problem-details-error pattern exactly. On success:
  `authService.login()` then navigate to `/`. On failure: generic message
  (matches the backend's generic 401), same `kind: 'server'` error-mapping
  pattern already used in `challenge-form.ts`.
- Modify `app.routes.ts` — add `{ path: 'login', component: LoginPage }`,
  unguarded.
- Per decision #7: modify `app.html`/`app.ts` to add a minimal top bar shown
  app-wide, with a logout button ("Abmelden", per `web/CLAUDE.md`'s
  German-UI-text rule) visible only when `authService.isAuthenticated()` is
  true — calls `authService.logout()` then navigates to `/login`.
- `login-page.spec.ts` following `challenge-form.spec.ts`'s existing pattern.

**Verify:** `ng test`/`ng lint`/`ng build` clean. Manual browser pass: log
in, hard-reload a guarded route (access token should silently restore via
the cookie), force/wait for a 401 to confirm the interceptor's
refresh-and-retry, log out and confirm redirected/blocked from guarded
routes, confirm `/health` still renders without a session.

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

## Critical files referenced throughout

- `api/Api/Program.cs` — DI + pipeline wiring for Identity/auth.
- `api/Api/Data/AppDbContext.cs` — Identity entity configuration.
- `api/Api/Infrastructure/ICurrentUserProvider.cs` /
  `DevCurrentUserProvider.cs` — replaced by `HttpCurrentUserProvider.cs`.
- `api/Api/Features/Challenges/ChallengeEndpoints.cs` — vertical-slice
  pattern to replicate for `Features/Auth/`.
- `api/Api.Tests/Infrastructure/IntegrationTestBase.cs` /
  `ApiFactory.cs` — need real-login setup and lowered test-hasher iterations
  once endpoints are protected.
- `api/Api/Features/Auth/RefreshTokenIssuer.cs` — the hand-rolled
  issue/rotate/revoke logic decision #9 depends on.
- `web/src/app/app.config.ts`, `web/src/app/app.routes.ts` — interceptor/
  guard/route wiring.
- `web/src/app/features/challenges/challenge-form.ts` — Signal Forms +
  problem-details pattern to replicate for the login form.
- `docs/decisions/0007-user-derives-identityuser.md` — what's already
  decided about `User`; this phase's ADRs must not contradict it.

## Process notes for execution

- One scoped stage per turn, per project convention — do not batch multiple
  stages into a single implementation turn.
- Backend (Stages 1-3) fully before frontend (Stages 4-6) — no interleaving.
- Stage 2's reuse-detection behavior (a rotated-away token revoking its whole
  family) needs an explicit test proving it, not just the happy-path
  rotation — this is the entire reason decision #9 chose the table approach.
