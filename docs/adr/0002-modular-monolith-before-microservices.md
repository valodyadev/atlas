# 0002. Modular monolith before microservices

- **Status:** Accepted
- **Date:** 2026-11-21

## Context

The system is expected to grow substantially, and the instruction was to
support "independently deployable services where justified". The tempting
reading is to start with services, one per bounded context, so that the split
never has to be done later.

That reading is backwards. At the start of a project the boundaries are least
well understood, and a service boundary is the most expensive kind to move: it
is a network hop, a deployment, a dashboard, an alert route, a secret, and a
versioned contract. Getting a module boundary wrong costs an afternoon of
refactoring. Getting a service boundary wrong costs a quarter.

Meanwhile the operational cost arrives on day one, long before the benefit: a
three-person team debugging a distributed trace across four services to find a
null reference.

## Decision

One deployable backend (`Atlas.Api`) composed of **modules** that are
service-shaped from the start:

- each module is its own project, with its own domain, persistence and endpoints;
- each module owns a **PostgreSQL schema**; no foreign key and no query crosses
  a schema boundary;
- modules never reference each other's internals — only published contracts;
- the host composes modules explicitly, in `Program.cs`.

These rules are enforced by tests in `backend/tests/Atlas.Architecture.Tests`,
not by convention. A violating project reference fails CI.

Extract a module into its own service when a *specific* pressure appears —
independent scaling, independent release cadence, team ownership, a different
runtime — and not before. Because the module already owns its schema and has
no sibling references, extraction is: new host project, call the same
`IApplicationModule`, point it at the same schema, move one line out of
`Program.cs`.

## Alternatives considered

| Option | Why not |
| --- | --- |
| Microservices from day one | Pays the full operational cost before the boundaries are understood; the boundaries would be guesses |
| A single project with folders | No enforcement: the first deadline produces a cross-context query, and after that the boundary is fiction |
| Modules sharing one schema | Removes the only thing that makes extraction cheap — a cross-schema join is invisible in review and fatal to a later split |

## Consequences

**Good**

- One thing to deploy, debug and trace while the team is small.
- Boundaries are real and enforced, so they can be moved cheaply while they
  are still wrong.
- Extraction is a mechanical change, not a rewrite.

**Bad / accepted costs**

- Everything scales together: a memory-hungry module forces the whole API to
  be sized for it.
- A single deploy pipeline means a release is blocked by whichever module is
  least ready.
- The no-cross-schema rule occasionally forces an in-process API call where a
  join would have been two lines.

**What this makes harder later**

- Nothing structurally — but the discipline is load-bearing. If the
  architecture tests are deleted or weakened, this ADR becomes a lie and the
  cheap-extraction property is gone.

## Revisit when

Any of: a module needs to scale independently; a module needs a different
release cadence; a team wants exclusive ownership of a deployment; a module
needs a runtime .NET cannot provide. Then extract **that** module — not all of
them.
