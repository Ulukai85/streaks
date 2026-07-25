# Troubleshooting

Local dev/tooling issues that aren't project bugs, kept here instead of
rediscovering them each time.

## Testcontainers fails with "Unauthorized: incorrect username or password"

**Symptom:** `dotnet test` fails pulling an image (Ryuk, or the test
container itself) with a `Docker.DotNet.DockerApiException` reporting
`Unauthorized` / "incorrect username or password" — even though `docker pull`
of the same image works fine from the CLI.

**Cause:** a non-standard `~/.docker/config.json` (e.g. separate
`access-token`/`refresh-token`-suffixed entries instead of one plain
`https://index.docker.io/v1/` entry). The Docker CLI tolerates this; the
Testcontainers .NET library's own registry-auth resolution does not, and
sends a bad auth header for pulls it initiates directly against the daemon.

**Fix:** point Docker tooling at a separate, empty config so unauthenticated
(anonymous) pulls are used instead:

```bash
mkdir -p ~/.docker-testcontainers
echo '{}' > ~/.docker-testcontainers/config.json
```

Add to your shell profile:

```bash
export DOCKER_CONFIG="$HOME/.docker-testcontainers"
```

If `docker ps` then fails with "cannot connect to the Docker daemon" —
`DOCKER_CONFIG` also relocates where the CLI reads its context from. Find the
real socket first (with the *old* config still active) and pin it explicitly:

```bash
docker context inspect --format '{{.Endpoints.docker.Host}}'
export DOCKER_HOST="unix:///var/run/docker.sock"   # or whatever that printed
```

**Tradeoff:** all `docker` commands become unauthenticated globally. Public
images (everything this project uses) are unaffected; for a one-off private
pull, override for that command only: `DOCKER_CONFIG=~/.docker docker pull ...`.

**Don't** work around this with `TESTCONTAINERS_RYUK_DISABLED=true`. That
disables Testcontainers' own cleanup container, so every crashed or
Ctrl-C'd test run leaves an orphaned Postgres container running — it hides
the symptom without fixing the auth problem underneath.

A version bump of a test image (e.g. `postgres:18-alpine`), or a first run
on a fresh machine, needs a manual `docker pull postgres:18-alpine` under
the working `DOCKER_CONFIG` above before `dotnet test` can pull it
automatically — Testcontainers .NET resolves auth against `DOCKER_CONFIG`,
not the CLI's own defaults, so a missing local image plus the anonymous-pull
config can otherwise produce a failure that looks unrelated to image
versioning.

## Compose Postgres is mapped to host port 5433, not 5432

This dev machine runs a native `postgresql.service` on 5432, so
`infrastructure/docker-compose.yml` maps Postgres to host `5433` instead
(`appsettings.Development.json`'s connection string matches). Don't "fix"
this back to 5432 without checking `5432` is actually free.

## `postgres:18-alpine` container reports incompatible data / fails healthcheck

**Cause:** the 18+ Postgres images expect a single volume mount at
`/var/lib/postgresql` (not `/var/lib/postgresql/data` as in older images) —
mounting the old path makes the entrypoint see "unused" data in the wrong
layout and refuse to start. `infrastructure/docker-compose.yml` already
mounts the volume at the correct path — if a future edit reverts this, the
`postgres` service will come up unhealthy.
