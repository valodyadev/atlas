# Local development

## First run

```bash
make bootstrap     # installs dependencies for whatever toolchains you have
make doctor        # reports what is present and what each missing tool blocks
make up            # Postgres, collector, API and the two workers
```

Then:

| What | Where |
| --- | --- |
| API reference (Scalar) | http://localhost:8080/scalar |
| OpenAPI document | http://localhost:8080/openapi/v1.json |
| Traces (Jaeger) | http://localhost:16686 |
| Node worker | http://localhost:8081/health/live |
| Python worker | http://localhost:8082/health/live |

`make bootstrap` is deliberately tolerant: a backend engineer without Xcode
and an iOS engineer without the .NET SDK can both run it and get a working
environment for their part of the repo.

## The loop you will actually use

Running everything in containers is slow to iterate on. Start only the backing
services and run the thing you are working on from your IDE:

```bash
make deps                                   # Postgres + collector + Jaeger only
cd backend && dotnet run --project src/Atlas.Api
```

The API reads `ConnectionStrings__Default` from your environment or `.env`.

### iOS

```bash
make ios-project          # generate ios/Atlas.xcodeproj from project.yml
open ios/Atlas.xcodeproj
```

Run `make ios-project` again after pulling a change that adds or removes
files — the project is generated and not committed
([ADR 0006](../adr/0006-ios-swift-packages-and-generated-project.md)).

The Debug configuration points at `http://localhost:8080/api/v1`. The
simulator reaches your machine's localhost directly; a **physical device** does
not — point `ATLAS_API_BASE_URL` in `Debug.xcconfig` at your machine's LAN
address, which the ATS exception in `Info.plist` already permits for local
networking.

Most iOS work needs no simulator at all:

```bash
make test-ios     # the whole unit suite, in seconds, no Xcode needed
```

## Tests

```bash
make test             # everything
make test-backend     # .NET
make test-ios         # Swift packages
make test-services    # Node + Python
```

### Why some backend tests say "skipped"

The integration tests start a real PostgreSQL container with Testcontainers.
Without a container runtime they **skip** rather than fail, so `make test`
works on a machine with no Docker:

```
skipped Atlas.Api.IntegrationTests.UserEndpointTests...
  No container runtime detected (Docker Desktop, Colima, OrbStack or Podman).
```

CI always has a runtime, so that coverage is never silently lost. To run them
locally, install Docker Desktop, Colima or OrbStack and re-run.

## Database

```bash
make migrate                                         # apply pending migrations
make migration NAME=AddOrdersTable MODULE=Identity   # create one
make migration-sql MODULE=Identity                   # print the idempotent SQL
```

Migrations are never applied by the API at startup — see
[the migrations runbook](database-migrations.md).

```bash
docker compose -f infra/docker/compose.yaml exec postgres psql -U atlas -d atlas
# \dn            list schemas (one per module)
# \dt identity.* the Identity module's tables
```

`make nuke` deletes the volume and gives you a clean database;
`make up` then re-migrates it.

## API contract

Regenerate after any endpoint or DTO change:

```bash
make openapi      # writes contracts/openapi/v1.json
```

CI fails if the committed contract differs from what the code produces, so
this is part of making an API change, not an afterthought.

## Formatting and linting

```bash
make lint      # check everything, change nothing
make format    # fix everything that can be fixed automatically
make install-hooks   # run the fast checks before each commit
```

## Troubleshooting

**`dotnet: command not found`** — the Homebrew formula is keg-only. `make`
adds it to `PATH` automatically; for your own shell:
`export PATH="/opt/homebrew/opt/dotnet@10/bin:$PATH"`.

**`ConnectionStrings:Default is not configured`** — the API validates this at
startup on purpose. `cp .env.example .env`, or export
`ConnectionStrings__Default`.

**The API refuses to start, complaining about `Auth:Enabled`** — also
deliberate. Only `Development` may run without authentication; any other
environment fails fast rather than serving unauthenticated traffic.

**`xcodebuild requires Xcode`** — only the Command Line Tools are installed.
The Swift packages still build and test; the app target needs full Xcode.

**Traces are not appearing in Jaeger** — the exporter is only attached when
`OTEL_EXPORTER_OTLP_ENDPOINT` is set. Check it points at
`http://localhost:4317`.

**Port already in use** — override in `.env`: `POSTGRES_PORT`,
`NODE_WORKER_PORT`, `PYTHON_WORKER_PORT`, `ASPNETCORE_HTTP_PORTS`.
