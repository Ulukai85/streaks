# ADR 0009 — `Microsoft.AspNetCore.Identity.EntityFrameworkCore` as the user store

Date: 2026-07-28
Status: Accepted

## Context

Phase 3 needs `UserManager<User>` and `SignInManager<User>` for password
hashing, verification, lockout, and normalized-username lookup. ADR 0007
already put `User : IdentityUser<Guid>` in place without a new package,
because `IdentityUser<TKey>` itself ships in the ASP.NET Core shared
framework. The *store* — the piece that actually persists those users through
EF Core — does not: `AddEntityFrameworkStores<AppDbContext>()` lives in
`Microsoft.AspNetCore.Identity.EntityFrameworkCore`, which `api/CLAUDE.md`
already flagged as "Phase 3's call". This is the one genuinely new NuGet
package this phase, hence this ADR.

## Decision

Add `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, pinned in
`Directory.Packages.props` at **10.0.10** to match the existing EF Core
family. Wire it as:

```csharp
builder.Services.AddIdentityCore<User>(options => { /* lockout */ })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();
```

`AddIdentityCore`, not `AddIdentity` — no cookie authentication scheme, no
`SignInManager` beyond the one explicitly added. **No `.AddRoles<TRole>()`**:
the store resolves to `UserOnlyStore` rather than `UserStore`, so EF Core
needs only `UserClaims`, `UserLogins`, and `UserTokens` — no `Roles`,
`UserRoles`, or `RoleClaims`.

`AppDbContext` stays a plain `DbContext`. It does **not** derive
`IdentityDbContext`/`IdentityUserContext`; the three Identity entities are
configured directly in `OnModelCreating`.

## Rationale

- Writing a custom `IUserStore<User>`/`IUserPasswordStore<User>` by hand is
  the only real alternative to this package, and it means reimplementing
  normalized-username lookup, concurrency stamps, and lockout persistence —
  materially more hand-written security-relevant code than the ~40 lines of
  EF configuration this package costs, for no benefit.
- **Not deriving `IdentityDbContext`** keeps table naming and column
  configuration under this project's explicit control: the tables are
  `UserClaims`/`UserLogins`/`UserTokens`, consistent with the existing
  `Users`/`Challenges`/`Completions`, rather than the framework's
  `AspNetUserClaims` etc. It also avoids inheriting a pile of role
  configuration for entities this app has no use for.
- The three Identity entities are configured **inline in `OnModelCreating`**
  rather than as `IEntityTypeConfiguration<T>` files under
  `Data/Configurations/`. Those files exist for this project's domain
  entities; these three are framework plumbing that nothing in
  `Api/Domain/` refers to, and grouping them in one block makes it obvious
  they arrive as a set.

## Consequences

- `UserClaims`, `UserLogins`, and `UserTokens` exist in the schema but are
  **unused** by the login/refresh/logout flow — they are there only because
  `UserOnlyStore`'s constructor requires them to be part of the EF model.
  They should stay empty; a row appearing in one is a signal that something
  unintended (external login, 2FA, claim persistence) got wired up.
- Refresh tokens are **not** stored via `UserTokens` or Identity's internal
  `RefreshTokenProtector`. They live in a hand-rolled `RefreshTokens` table
  (see the Phase 3 plan, decision #9) so that per-device logout and
  reuse/replay detection are possible. This package is used for password
  handling and user lookup only.
- Adding roles later means calling `.AddRoles<TRole>()`, adding three more
  entity configurations, and a migration — deliberately deferred, since v1
  is single-account.
