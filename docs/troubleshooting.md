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
