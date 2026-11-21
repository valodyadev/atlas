# Architecture

How the system is shaped, and the rules that keep it that way. Decisions and
their reasoning live in [`adr/`](adr/); this page is the map.

## Shape

```
┌──────────────────┐
│   iOS app        │  SwiftUI shell + Swift packages (ios/)
└────────┬─────────┘
         │ HTTPS, /api/v1, contract in contracts/openapi/
┌────────▼─────────────────────────────────────────────┐
│  Atlas.Api                      (backend/src)        │
│  ┌────────────────────────────────────────────────┐  │
│  │ transport: versioning, auth, rate limit, docs  │  │
│  ├────────────────────────────────────────────────┤  │
│  │ Modules.Identity   │  Modules.<Next>   │  ...  │  │
│  │  schema: identity  │  schema: <next>   │       │  │
│  └────────────────────────────────────────────────┘  │
│  ServiceDefaults: telemetry, health, resilience      │
│  SharedKernel:    domain primitives, zero deps       │
└──────┬───────────────────────────────┬───────────────┘
       │                               │ OTLP
┌──────▼──────┐              ┌─────────▼──────────┐
│ PostgreSQL  │              │  OTel Collector    │──▶ traces/metrics/logs
│ one schema  │              └────────────────────┘    backend of choice
│ per module  │
└─────────────┘
       ▲
┌──────┴───────────┐        ┌──────────────────────┐
│ Atlas.DbMigrator │        │ services/            │
│ job, not startup │        │  node-worker         │
└──────────────────┘        │  python-worker       │
                            └──────────────────────┘
```

## The five rules

Everything else follows from these. The first four are enforced by tests in
[`backend/tests/Atlas.Architecture.Tests`](../backend/tests/Atlas.Architecture.Tests),
so breaking one fails CI rather than review.

1. **A module owns its schema outright.** No query, join or foreign key
   crosses a schema boundary. This is the single property that makes
   extracting a module into its own service cheap.
2. **Modules never reference each other.** They talk through published
   contracts. Shared behaviour moves down into `SharedKernel` or
   `ServiceDefaults`, never sideways.
3. **Dependencies point inward.** Domain code knows nothing about EF Core,
   ASP.NET Core or HTTP. `SharedKernel` has zero dependencies — not even the
   web framework.
4. **The host owns no domain logic and no persistence.** `Atlas.Api` is a
   composition root: a list of modules and the transport concerns that apply
   to all of them.
5. **Modules are registered explicitly.** No assembly scanning. The
   dependency graph is `Program.cs`, greppable and deterministic, and deleting
   a module is a compile error rather than a silent behaviour change.

## Layout

| Path | What it is |
| --- | --- |
| `backend/src/Atlas.SharedKernel` | Domain primitives: `Entity`, `Result`, `Error`, `Page`, `IClock`. Zero dependencies. |
| `backend/src/Atlas.ServiceDefaults` | What every .NET entry point calls: OTel, health, resilience, problem+json, the module contract. |
| `backend/src/Atlas.Api` | The HTTP host. Versioning, auth, rate limiting, OpenAPI, and the module list. |
| `backend/src/Atlas.DbMigrator` | Applies migrations and exits. Never run from the API. |
| `backend/src/Modules/*` | One vertical slice each: Domain → Application → Infrastructure → Endpoints. |
| `ios/Packages/AtlasKit` | All iOS code, as layered Swift package targets. |
| `ios/App` | The app shell: entry point, composition root, navigation, Keychain. |
| `services/*` | Services in other runtimes. Admission criteria in [`services/README.md`](../services/README.md). |
| `contracts/openapi` | The committed API contract. CI fails if it drifts from the code. |
| `infra/` | Compose for local, Kustomize for Kubernetes, collector config. |

## Adding a module

The Identity module is the reference; copy its shape.

1. `dotnet new classlib -o backend/src/Modules/Atlas.Modules.Billing`
2. Reference `Atlas.SharedKernel` and `Atlas.ServiceDefaults`. **Not** another
   module.
3. Implement `IApplicationModule`: `ConfigureServices` registers the
   `DbContext` and services; `MapEndpoints` maps the routes.
4. Give it its own schema constant and its own migrations-history table, both
   inside that schema.
5. Add one line to `builder.AddModules(...)` in `Program.cs`, and one entry to
   `MigrationRunner.Migrations`.
6. Add the namespace prefix to `ModuleNamespacePrefixes` in
   `ModuleBoundaryTests` — the list is part of the rule, and a module missing
   from it is not checked against its siblings.
7. `make migration NAME=InitialBillingSchema MODULE=Billing`

## Extracting a module into a service

Because of rule 1 and rule 2, this is mechanical:

1. New host project; call `AddServiceDefaults()` and `AddModules(new BillingModule())`.
2. Point it at the same database, or move the `billing` schema to its own
   database — no query changes either way, because nothing joined across it.
3. Remove the module from the API's `AddModules(...)` call.
4. Replace in-process calls with HTTP calls to the published contract.
5. Add a deployment, dashboards, alert routes and an on-call runbook. This
   step is the expensive one, which is why [ADR 0002](adr/0002-modular-monolith-before-microservices.md)
   says to wait for a specific pressure before taking it.

## Request path

`Forwarded headers → exception handler (problem+json) → rate limiter →
authentication → authorization → endpoint`

The order is load-bearing. Forwarded headers come first so logging and rate
limiting see the real client; the exception handler wraps everything after it;
rate limiting precedes authentication so an unauthenticated flood is cheap to
reject.

## Cross-cutting behaviour

| Concern | Where | Notes |
| --- | --- | --- |
| Configuration | `appsettings.json` → `appsettings.{Env}.json` → user secrets (dev) → environment → command line | No secrets in any committed file. See [security/secrets.md](security/secrets.md). |
| Errors | `ServiceDefaults/ErrorHandling` | RFC 9457 everywhere, including the Node and Python services. Stable dotted `code`, plus `traceId`. |
| Health | `ServiceDefaults/Diagnostics` | `/health/live` never touches a dependency; `/health/ready` does. Mixing them turns a database blip into a fleet restart. |
| Telemetry | `ServiceDefaults/Observability` | OTLP only. [ADR 0005](adr/0005-opentelemetry-over-otlp.md). |
| Resilience | `AddServiceDefaults` | Every `HttpClient` gets retries, a circuit breaker and timeouts by default. |
| Auth | `Api/Extensions/AuthenticationExtensions.cs` | Secure by default: a fallback policy requires a token, so a new endpoint is protected unless it opts out. The API refuses to start with auth disabled outside Development. |
| Rate limiting | `Api/Extensions/RateLimitingExtensions.cs` | In-process backstop, partitioned per subject then per IP. Not a replacement for an edge limit. |

## Known gaps

Deliberate, and worth knowing before you build on this:

- **No identity provider.** `Auth:Enabled=false` in Development; the API
  refuses to start without auth anywhere else. Wire up an issuer and
  implement `KeychainTokenStore.refreshToken()`.
- **No crash reporting on iOS.** `OSLogTelemetry` writes to the device log,
  which is not a substitute. First dependency to add — see
  [ADR 0007](adr/0007-ios-dependency-policy.md).
- **Domain events are raised but not dispatched.** `AggregateRoot.Raise`
  collects them and `UserService` clears them. Wiring a dispatcher — ideally
  with a transactional outbox — is the next step if you need them.
- **Deployment is a stub.** `release.yml` builds and pushes signed images with
  SBOMs; the deploy job prints what it would do. Filling it in requires
  cluster and credential decisions this repo cannot make for you.
- **No Terraform.** `infra/terraform/` is a placeholder; the Kustomize
  overlays assume a cluster already exists.
