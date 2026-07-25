# ADR 0007 — `User` derives `IdentityUser<Guid>` ahead of Phase 3

Date: 2026-07-25
Status: Accepted

## Context

Phase 2 needs a minimal `User` table now (`Id`, `TimeZoneId`) with no
credentials or auth. Phase 3 adds real authentication via ASP.NET Core
Identity. `Challenge`/`Completion` both carry a `UserId` FK, and changing a
primary-key type after the fact means touching every FK pointing at it plus
migrating any existing rows — expensive enough that the eventual shape of
`User` is worth deciding now, even though Identity itself (`AddIdentity`,
`UserManager`, login/password flows) isn't wired up until Phase 3.

Two shapes were viable: define `User` as a plain POCO now and either migrate
it to `IdentityUser<Guid>` later, or keep a separate `AppUser` domain entity
joined to an Identity user by id.

## Decision

`User : IdentityUser<Guid>`, adding only `TimeZoneId`. No `AddIdentity()`,
no `UserManager`, no auth middleware, no password/login flow this phase —
purely the entity shape and its EF mapping.

## Rationale

- Avoids a second, FK-touching migration in Phase 3 and a data-migration
  story for the one seeded dev row.
- The extra Identity columns (`UserName`, `Email`, `PasswordHash`,
  `SecurityStamp`, `LockoutEnd`, etc.) are all nullable/default-valued in
  modern ASP.NET Core Identity, so they sit unused at no cost until Phase 3
  configures them for real.
- **No new NuGet dependency.** `IdentityUser<TKey>` (`Microsoft.AspNetCore.Identity`
  namespace) ships as part of the ASP.NET Core shared framework; a
  `Microsoft.NET.Sdk.Web` project (`Api.csproj`) references it implicitly via
  the `Microsoft.AspNetCore.App` framework reference. Confirmed by a clean
  `dotnet build` with no new `PackageReference` added — not asserted from
  documentation alone.
- Rejected **separate `AppUser` domain entity** joined by id: cleaner
  domain/auth separation in principle, but defers the join design to Phase 3
  and doesn't remove the PK-type risk this ADR exists to close off — Identity
  would still need a `Guid`-keyed user store either way.

## Consequences

- The `Users` table carries dormant Identity columns (`PasswordHash`,
  `ConcurrencyStamp`, `LockoutEnd`, etc.) from Stage 1 onward, unconfigured
  until Phase 3.
- `IdentityUser<Guid>`'s parameterless constructor does not auto-populate
  `Id` the way the non-generic, string-keyed `IdentityUser` does — callers
  must set `Id` explicitly. The Stage 1 dev-user seed does this; Phase 3's
  registration flow will need to as well.
- Reversing this decision later means changing the `User` entity and every
  FK referencing it — accepted as the cost of avoiding two migrations.
