# CLAUDE.md

Guidance for Claude Code in this repo. Source of truth is `PROJECT-BRIEF.md` —
read it before doing anything non-trivial. This file just orients you.

## Where things live

- `README.md` — how to run/deploy the stack (Docker Compose, local dev,
  production).
- `infrastructure/` — `docker-compose.yml`, `docker-compose.prod.yml`
  (server-only GHCR image override, see ADR 0010), `Caddyfile`,
  `deploy.sh`, `.env.example` for the deployable stack (Postgres + api +
  Caddy).
- `.github/workflows/` — CI (`ci.yml`, every PR + push) and CD
  (`deploy.yml`, GHCR build-push + SSH deploy on `main` once CI is green).
- `docs/domain.md` — the period/streak domain model, restated as prose. Read
  this before touching anything cadence- or streak-related.
- `docs/phase-2-plan.md` / `docs/phase-3-plan.md` / `docs/phase-4-plan.md` —
  working plans tracking stage-by-stage status as each phase lands; check
  here for what's done vs. still ahead.
- `docs/decisions/` — ADRs. One is required before any new dependency.
- `docs/aspnet-conventions.md` — backend code organization conventions.
- `docs/angular-conventions.md` — frontend code organization conventions.
- `docs/troubleshooting.md` — local dev/tooling issues (Docker, IDE) that
  aren't project bugs.
- `docs/existing-problems.md` — known unresolved issues (e.g. upstream
  dependency vulnerabilities) with the reasoning for leaving them as-is.
- `api/CLAUDE.md` — backend rules (.NET/EF Core specifics).
- `web/CLAUDE.md` — frontend rules (Angular specifics).

## MCP servers

- Angular-specific questions (docs, best practices, code examples,
  migrations) go through the **Angular CLI MCP server**, not context7.
- Everything else (other libraries, frameworks, SDKs, CLIs, cloud
  services) keeps using **context7** as usual.
- If an MCP query surfaces something durably relevant to this project,
  write it concisely into the relevant CLAUDE.md/conventions file so the
  server doesn't need to be queried again for the same fact.

## Non-negotiables (see PROJECT-BRIEF.md §2–3 for full list + rationale)

- No reminders/notes/quantities/categories/social/mobile/import-export in v1.
- No MediatR/CQRS, no repository pattern over EF Core, no NgRx, no
  microservices/GraphQL.
- No new NuGet/npm dependency without an ADR first.

## Process

- Interview the human to identify the goal of a feature or task before writing code.
- Favor smaller and more compartmentalized specs over larger ones; one scoped task per turn.
- Stop and ask rather than inventing a domain rule not stated in the brief.
- Make the human verify key decisions explicitly to ensure nothing is missed.
- Plan before implementing anything non-trivial; human reads the plan first.
- Outline the evaluation criteria you will use to ensure a high quality final product.
- Before you do any work, mention how you could verify that work.
