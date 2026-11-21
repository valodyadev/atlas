# 0003. .NET as the primary backend, with room for other runtimes

- **Status:** Accepted
- **Date:** 2026-11-21

## Context

C#/.NET was specified as the primary backend, with the architecture staying
open to independent Node.js or Python services. The risk in "open to other
runtimes" is that it becomes an invitation: each service added in a different
language multiplies the build tooling, the base images, the dependency
scanners, the logging shapes, the on-call knowledge and the hiring surface.

The second risk is the opposite failure: a rigid .NET-only rule that forces
someone to reimplement a mature Python ML library in C#.

## Decision

`backend/` is the system of record and the default home for new work. .NET 10
is the pinned target (LTS, via `backend/global.json`).

`services/` holds services in other runtimes, admitted only when one of these
is true:

1. the best library for the job does not exist in .NET (ML, scientific
   computing, a vendor SDK with no .NET client);
2. the workload has a genuinely different scaling or resource profile;
3. it must be deployed and rolled back independently of the API.

"The team prefers that language" is not on the list.

Every service, whatever its runtime, must satisfy the seven rules in
[`services/README.md`](../../services/README.md): the same health-probe split,
OTLP telemetry, environment-only configuration, RFC 9457 errors, structured
stdout logging, a non-root container, and no writes to another service's
schema. Uniform *operational* behaviour is what keeps a polyglot estate
affordable; uniform language is not required.

## Alternatives considered

| Option | Why not |
| --- | --- |
| .NET only, no exceptions | Forces reimplementation of mature libraries; the cost lands on whoever needs ML or data tooling |
| Free choice per service | Multiplies the toolchain, image and on-call surface without a forcing reason |
| A service mesh to paper over the differences | Buys uniform networking, not uniform operability; adds a large component to a small system |

## Consequences

**Good**

- One language for the overwhelming majority of the code, so engineers move
  between modules freely.
- A genuine escape hatch with an explicit, arguable bar.
- Uniform operational contract, so a new service is immediately observable and
  debuggable by whoever is on call.

**Bad / accepted costs**

- Three CI pipelines and three dependency ecosystems already exist, for two
  reference services that currently do nothing. That is deliberate: the cost
  of the pattern is visible up front rather than discovered on the first real
  service.
- The seven rules must be re-implemented per runtime; there is no shared
  library to enforce them.

**What this makes harder later**

- Cross-cutting changes (a new required header, a new auth scheme) must be made
  once per runtime.

## Revisit when

A third runtime is proposed, or when the reference services have been unused
for long enough that deleting them is cheaper than maintaining them.
