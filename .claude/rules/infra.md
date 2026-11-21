---
paths:
  - "infra/**"
  - "**/Dockerfile"
  - ".github/**"
  - "Makefile"
---

# Infrastructure rules

Owned by `atlas-devops`.

Loaded on demand when a Dockerfile, a compose or Kustomize file, a workflow or
the Makefile is read or edited.

## Already-paid-for mistakes

- **Postgres 18 changed its data directory.** Mount the volume at
  `/var/lib/postgresql`, **not** `/var/lib/postgresql/data`. The image now keeps
  the cluster in a major-version subdirectory so `pg_upgrade --link` does not
  cross a mount boundary. The old path makes the container refuse to start.
- **The root `.dockerignore` is load-bearing, not an optimisation.** The .NET
  Dockerfiles restore in an early stage and then `COPY backend/ ./backend/`
  before `dotnet publish --no-restore`. Without the `**/artifacts/` and
  `**/obj/` exclusions, that COPY drops the host's restore output over the
  container's and the publish fails with `NETSDK1064`. It also keeps the context
  at ~1 MB instead of ~1 GB.
- **`Atlas.DbMigrator` needs the aspnet image**, not `runtime` — see
  `.claude/rules/backend.md`.
- The Postgres image tag is pinned in two places that must agree:
  `infra/docker/compose.yaml` and `PostgresFixture.PostgresImage` in the
  integration tests. If they drift, the tests stop telling you anything about
  what you deploy.
- `docker compose` needs the compose plugin; the Homebrew `docker` formula is
  only the CLI. Symlink `docker-compose` into `~/.docker/cli-plugins/`.

## Kubernetes

- Liveness never touches a dependency; readiness does. Inverting them turns a
  brief database blip into a fleet-wide restart.
- Migrations run as a Job that must succeed **before** the API rolls. Never at
  API startup.
- No image tag is `latest`; CI sets an immutable digest.
- No secret in any manifest. `secretRef` points at a Secret an external
  operator creates.

## CI

GitHub Actions is currently **blocked by an account billing lock**, so no
workflow has executed. Do not treat a green or absent CI status as evidence.
Workflow files are therefore unverified end to end.
