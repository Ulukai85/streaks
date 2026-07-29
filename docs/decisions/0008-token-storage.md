# ADR 0008 — Token storage: httpOnly refresh cookie, in-memory access token

Date: 2026-07-28
Status: Accepted

## Context

Phase 3 (`PROJECT-BRIEF.md` §11) requires an ADR on token storage before any
auth code is written. The app issues two tokens with different lifetimes and
different threat exposure: a short-lived access token sent on every API call,
and a long-lived refresh token whose only job is to mint new access tokens.
Where each is kept decides what an XSS bug in the Angular app can steal.

The realistic options for a browser SPA are: both tokens in
`localStorage`/`sessionStorage` (readable by any script on the origin), both
in httpOnly cookies (unreadable by script, but then the access token rides
along on every request as ambient authority and needs CSRF defence), or the
split adopted below.

## Decision

- **Refresh token** — set by the backend as a cookie with `HttpOnly`,
  `Secure`, `SameSite=Strict`, `Path=/api/auth`, and **no explicit `Domain`**
  (host-only). JavaScript never sees it; it is never present in any response
  body. `LoginResponse` deliberately has no `RefreshToken` field.
- **Access token** — returned in the login/refresh response body and held
  **only** in an in-memory Angular signal. Never written to `localStorage`,
  `sessionStorage`, `IndexedDB`, or a cookie. It dies with the tab.
- On a hard reload the access token is gone, so the app performs one silent
  `POST /api/auth/refresh` (the cookie is still there) before rendering a
  guarded route.

## Rationale

- **XSS cannot read the refresh token.** `HttpOnly` is the one storage
  property script cannot work around. An XSS bug can still use the in-memory
  access token for as long as the page lives, but it cannot exfiltrate a
  30-day credential — the blast radius is bounded by the 15-minute access
  token instead of by the refresh token's lifetime.
- **`SameSite=Strict` is the CSRF defence, and it is free here** because of
  the deploy topology this phase assumes (see Preconditions): the browser
  simply never attaches the cookie to a cross-site request, so no anti-CSRF
  token pair is needed. `Path=/api/auth` narrows it further — the cookie is
  not even sent on ordinary `/api/challenges` calls, only on the three
  endpoints that need it.
- Rejected **both tokens in `localStorage`**: simplest to implement and
  survives reload with no round trip, but any XSS trivially reads a long-lived
  refresh token and the session is compromised indefinitely. This is the
  failure mode the whole decision exists to avoid.
- Rejected **access token in a cookie too**: removes the silent-refresh round
  trip, but makes the access token ambient authority on every request, which
  reintroduces CSRF surface across the entire API rather than confining it to
  `/api/auth`, and fights the `Authorization: Bearer` handler ASP.NET Core
  Identity's bearer scheme already provides.

## Preconditions

This decision assumes the **same-origin production topology** of Phase 4:
Caddy serves the built Angular app and reverse-proxies `/api/*` to the
backend under one domain. `SameSite=Strict` on the refresh cookie is only
safe — indeed only functional — under that shape. If the frontend is ever
served from a different origin than the API, this ADR must be revisited:
`Strict` would have to become `None` and an explicit CSRF defence would
become mandatory.

## Consequences

- One extra round trip on every hard reload / new tab before a guarded route
  can render. Accepted.
- `Secure` must be relaxed in Development only, because local dev runs over
  plain HTTP. That relaxation is environment-gated, never unconditional.
- The refresh flow is entirely cookie-driven: `POST /api/auth/refresh` and
  `POST /api/auth/logout` read the cookie, not a request body. The Angular
  client sets `withCredentials: true` on those calls.
- Closing the tab ends the in-memory session but not the server-side refresh
  token; the token stays valid until it expires or is revoked. Per-device
  logout and reuse detection are handled by the refresh-token table, not by
  this ADR.
