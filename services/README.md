# Satellite services

The .NET backend in [`backend/`](../backend) is the primary system of record.
Services here exist for work that is genuinely better served by another
runtime, and each one is independently deployable.

## When a service belongs here

Add a service only when at least one of these is true:

| Reason | Example |
| --- | --- |
| The best library for the job only exists in that ecosystem | ML inference, scientific computing, a vendor SDK with no .NET client |
| The workload has a fundamentally different scaling profile | a long-running stream consumer; a bursty, memory-hungry batch job |
| It must be deployed and rolled back independently of the API | a third-party webhook receiver with its own uptime contract |

**Not** reasons: "the team prefers TypeScript", "this feature feels separate",
"microservices are best practice". A new module inside the .NET backend costs
one project reference. A new service costs a deployment, a dashboard, an alert
route, a secret store entry, an on-call runbook and a network hop — forever.
See [ADR 0003](../docs/adr/0003-dotnet-primary-backend-with-polyglot-services.md).

## Non-negotiables for every service

Whatever the language, a service in this repo must:

1. **Expose `/health/live` and `/health/ready`** with the same split as the
   .NET services — liveness never touches dependencies.
2. **Emit OpenTelemetry** over OTLP, honouring the standard `OTEL_*`
   environment variables, so it appears in the same traces as the API.
3. **Read all configuration from the environment.** No config files with
   secrets, no baked-in endpoints.
4. **Return RFC 9457 `application/problem+json`** for errors, so clients get
   one error shape across the whole platform.
5. **Log structured JSON to stdout.** The platform collects stdout; files do not.
6. **Ship a non-root container** with a pinned base image.
7. **Own no tables in another service's schema.** Cross-service reads go
   through an API, never through a shared database.

## Services

| Service | Runtime | Port | Purpose |
| --- | --- | --- | --- |
| [`node-worker`](./node-worker) | Node 22 + Fastify + TypeScript | 8081 | Reference TypeScript service |
| [`python-worker`](./python-worker) | Python 3.13 + FastAPI | 8082 | Reference Python service |

Both are deliberately empty reference implementations: they demonstrate the
seven rules above and nothing else. Delete the one you do not need.
