# Atlas

A production-shaped starter for an iOS app and its backend, built to grow.

- **iOS** — SwiftUI, Swift 6 strict concurrency, modularised as local Swift packages
- **Backend** — C#/.NET 10 (LTS), a modular monolith with service-shaped module boundaries
- **Satellite services** — Node 22 and Python 3.13 reference services, for work that genuinely belongs in another runtime
- **Platform** — PostgreSQL, EF Core migrations as a deploy step, OpenTelemetry, Docker Compose, Kustomize, GitHub Actions

> `Atlas` is a placeholder name. See [Renaming](#renaming) to make it yours.

---

## Quick start

```bash
make bootstrap    # install dependencies for whatever toolchains you have
make doctor       # report what is present, and what each missing tool blocks
make up           # start Postgres, the collector, the API and both workers
```

| | |
| --- | --- |
| API reference | http://localhost:8080/scalar |
| OpenAPI document | http://localhost:8080/openapi/v1.json |
| Traces | http://localhost:16686 |

Then `make test`, and `make` on its own to list every target.

Nothing here is mandatory: `make doctor` tells you exactly what each missing
tool disables. A backend engineer without Xcode and an iOS engineer without
the .NET SDK can both work in this repo.

## Layout

```
backend/        C#/.NET 10 - the system of record
  src/
    Atlas.Api             HTTP host: versioning, auth, rate limits, OpenAPI
    Atlas.ServiceDefaults telemetry, health, resilience, module contract
    Atlas.SharedKernel    domain primitives, zero dependencies
    Atlas.DbMigrator      applies migrations and exits
    Modules/Identity      reference module: one schema, one vertical slice
  tests/                  unit, architecture and Testcontainers integration tests

ios/            SwiftUI app
  Packages/AtlasKit       all the code: Core, Networking, DesignSystem, Features
  App/                    shell: entry point, composition root, config, Keychain
  project.yml             XcodeGen spec - the .xcodeproj is generated, not committed

services/       other runtimes, admitted on evidence (see services/README.md)
contracts/      the committed OpenAPI contract; CI fails if it drifts
infra/          compose, Kustomize overlays, collector config, terraform placeholder
docs/           architecture, ADRs, runbooks, security
tools/          scripts and git hooks
```

## How it is put together

Five rules hold the backend's shape, and the first four are enforced by tests
rather than by convention:

1. A module **owns its PostgreSQL schema** outright — no query or foreign key
   crosses a schema boundary.
2. Modules **never reference each other**.
3. Dependencies **point inward**; `SharedKernel` has zero dependencies.
4. The host owns **no domain logic and no persistence**.
5. Modules are registered **explicitly** — no assembly scanning.

Together they mean extracting a module into its own service later is a
mechanical change rather than a rewrite — which is why this starts as one
deployable instead of six. The reasoning is in
[ADR 0002](docs/adr/0002-modular-monolith-before-microservices.md).

**Start here:** [`docs/architecture.md`](docs/architecture.md), then the
[ADRs](docs/adr/) for why each decision went the way it did — including what
each one costs.

## Everyday commands

| | |
| --- | --- |
| `make up` / `make down` / `make nuke` | run the local stack; `nuke` deletes data |
| `make deps` | only Postgres, the collector and Jaeger — run the app from your IDE |
| `make test` | every suite; `test-backend`, `test-ios`, `test-services` individually |
| `make lint` / `make format` | check / fix across all four languages |
| `make migration NAME=X MODULE=Identity` | create a migration |
| `make migrate` | apply pending migrations |
| `make openapi` | regenerate the committed API contract |
| `make ios-project` | generate `ios/Atlas.xcodeproj` |
| `make doctor` | toolchain report |

## Prerequisites

Installed by `make bootstrap` where possible; `make doctor` reports the rest.

| Tool | Needed for | Install |
| --- | --- | --- |
| .NET 10 SDK | API, migrations, backend tests | `brew install dotnet@10` |
| Docker / Colima / OrbStack | local stack, backend integration tests | Docker Desktop, or `brew install colima docker docker-compose && colima start` |
| Xcode 16.4+ | building the iOS **app**, simulator tests | App Store |
| Swift toolchain | the iOS **packages** (no Xcode needed) | Xcode or Command Line Tools |
| XcodeGen | generating the Xcode project | `brew install xcodegen` |
| SwiftFormat / SwiftLint | iOS formatting and linting | `brew install swiftformat`; SwiftLint from its `portable_swiftlint.zip` release |
| Node 22 | the Node worker | `brew install node` |
| uv | the Python worker | `brew install uv` |
| kustomize | rendering Kubernetes manifests | `brew install kustomize` |

The Homebrew .NET formula is keg-only. `make` adds it to `PATH` for you; for
your own shell, `export PATH="/opt/homebrew/opt/dotnet@10/bin:$PATH"`.

SwiftLint requires a **full Xcode** install (for `sourcekitdInProc.framework`),
so `make lint` skips it when only the Command Line Tools are present. Note that
`brew install swiftlint` fails on macOS 27, which Homebrew treats as a
pre-release Tier 2 configuration — use the official `portable_swiftlint.zip`
release instead.

## Git

This repository is **not** initialised as a git repo — everything needed is in
place, so that the first commit is yours:

```bash
make git-init        # git init -b main, no commit
make install-hooks   # fast pre-commit checks: secrets and formatting
git add -A && git commit -m "Initial commit: Atlas starter"
```

Already configured: `.gitignore`, `.gitattributes`, `CODEOWNERS`, a pull
request template, an issue template, Dependabot, and five CI workflows.

[`.github/CODEOWNERS`](.github/CODEOWNERS) assigns everything to
`@valodyadev`. If this repository moves into an organisation, switch it to
per-area teams — team syntax does not work on a personal account.

## CI/CD

| Workflow | Runs on | Does |
| --- | --- | --- |
| `backend.yml` | changes under `backend/` | locked restore, format check, build, tests (integration tests really run — the runner has Docker), contract-drift check |
| `ios.yml` | changes under `ios/` | `swift build`/`swift test` with warnings as errors, SwiftFormat, SwiftLint, then generate the project and build the app for the simulator |
| `services.yml` | changes under `services/` | per-service typecheck, lint, format and tests |
| `security.yml` | everything, plus weekly | gitleaks over full history, dependency review, CodeQL |
| `release.yml` | `v*.*.*` tags | builds and pushes signed, SBOM-attested images to GHCR; **the deploy job is a stub** |

Path filters and concurrency groups mean an iOS change does not queue a
backend run, and a superseded push is cancelled.

## Renaming

`Atlas` appears in namespaces, project names, bundle identifiers and image
names. To rename:

1. `grep -ril atlas . --exclude-dir={node_modules,.build,.venv,artifacts,obj,bin}`
2. Rename the `backend/src/Atlas.*` directories and `.csproj` files, and
   update `backend/Atlas.slnx`.
3. Rename `ios/Packages/AtlasKit` and its targets in `Package.swift`.
4. Set `PRODUCT_BUNDLE_IDENTIFIER` in `ios/App/Config/*.xcconfig` and
   `bundleIdPrefix` in `ios/project.yml`.
5. Update the image names in `.github/workflows/release.yml` and
   `infra/k8s/`.

Worth doing before the first commit, and awkward afterwards.

## Known gaps

Deliberate, and listed so you do not discover them at an inconvenient moment:

- **No identity provider.** Auth is wired but disabled in Development; the API
  refuses to start with it disabled anywhere else. Point `Auth:Authority` at an
  issuer and implement `KeychainTokenStore.refreshToken()`.
- **No crash reporting on iOS.** `OSLogTelemetry` writes to the device log,
  which is not a substitute.
- **Domain events are raised but not dispatched.** A dispatcher — ideally with
  a transactional outbox — is the next step if you need them.
- **Deployment is a stub.** Images are built, pushed, signed and attested; the
  deploy job prints what it would do, because the cluster and credential
  decisions are yours.
- **No Terraform.** `infra/terraform/` is a placeholder; the Kustomize
  overlays assume a cluster exists.
- **The two satellite services do nothing.** They are reference
  implementations of the operational contract. Delete the one you do not need.

## Documentation

| | |
| --- | --- |
| [Architecture](docs/architecture.md) | the map, the five rules, how to add or extract a module |
| [ADRs](docs/adr/) | every significant decision, its alternatives and its costs |
| [API conventions](docs/api/README.md) | versioning, errors, pagination, auth, health |
| [Local development](docs/runbooks/local-development.md) | the daily loop and troubleshooting |
| [Database migrations](docs/runbooks/database-migrations.md) | expand/contract, backfills, failure recovery |
| [Secrets](docs/security/secrets.md) | where configuration lives, and what to do if one leaks |
| [Adding a service](services/README.md) | the bar a new runtime has to clear |
| [Agentic workflow](docs/workflow/README.md) | the four agents, the five stages, example prompts and limitations |
| [Open items](docs/workflow/open-items.md) | live risks, open questions and deferred work |
| [Contributing](CONTRIBUTING.md) | the workflow and what review looks for |
