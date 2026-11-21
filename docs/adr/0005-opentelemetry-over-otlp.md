# 0005. OpenTelemetry over OTLP, no vendor SDK in application code

- **Status:** Accepted
- **Date:** 2026-11-21

## Context

Observability vendors are chosen early, with little data, and changed later,
with much more. If a vendor's SDK is imported throughout the application,
"change vendor" becomes a cross-cutting refactor of every service, and the
decision quietly becomes permanent.

A polyglot estate makes this worse: four runtimes times one vendor SDK each is
four integrations to keep in step, and four ways for a trace to be broken.

## Decision

Application code depends only on **OpenTelemetry** and emits **OTLP**. No
vendor SDK appears in any service.

A **collector** sits between the services and the backend and decides where
data goes. Changing vendor, adding a second destination, or sampling
differently is a change to
[`infra/observability/otel-collector.yaml`](../../infra/observability/otel-collector.yaml)
and a collector redeploy — no application change, no release.

Standard `OTEL_*` environment variables are honoured everywhere, because every
hosting platform and sidecar already sets them. When no endpoint is
configured, the exporter is not attached at all, so local runs stay quiet
instead of logging an export failure every few seconds.

Health and metrics probes are excluded from tracing in every service. They are
the highest-volume and lowest-value spans in the system.

## Alternatives considered

| Option | Why not |
| --- | --- |
| A vendor SDK per service | Locks in the vendor across four runtimes; the SDK is on the hot path of every request |
| Logs only | Cannot answer "which call in this request was slow?" in a system with more than one hop |
| Export OTLP directly to the vendor, no collector | Works, but puts the vendor's endpoint and sampling policy in every deployment's configuration |

## Consequences

**Good**

- Vendor choice is deferred and reversible.
- One telemetry shape across .NET, Node and Python; traces join up across the
  whole platform.
- Logs carry trace and span ids, so a user-visible error can be pivoted to the
  trace that produced it.

**Bad / accepted costs**

- The collector is one more component to run, scale and monitor. If it is
  down, telemetry is lost — it must not be in the request path.
- OTLP semantic conventions are still evolving, so dashboards occasionally
  need attribute renames.

**What this makes harder later**

- Vendor-specific features (a proprietary profiler, RUM, a session replay)
  still require that vendor's SDK. Those are additive and local, not
  cross-cutting.

## Revisit when

A vendor feature is worth the lock-in, or the collector becomes an operational
burden out of proportion to a system this size.
