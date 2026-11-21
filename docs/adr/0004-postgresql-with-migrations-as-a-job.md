# 0004. PostgreSQL, with migrations applied by a job

- **Status:** Accepted
- **Date:** 2026-11-21

## Context

Two decisions, tightly coupled: the database, and *when* schema changes are
applied.

The second matters more than it looks. `dbContext.Database.Migrate()` at
application startup is the default in most tutorials, and it breaks in three
predictable ways: two replicas rolling simultaneously both try to migrate; a
slow migration turns into a failed readiness probe and a restart loop; and a
migration cannot be reviewed, timed, or rolled back independently of a code
deploy.

## Decision

**PostgreSQL** as the relational store, with **EF Core** for mapping and
migrations. One schema and one `DbContext` per module, each with its own
migrations-history table inside its own schema.

Migrations are applied by **`Atlas.DbMigrator`**, a separate executable that
runs to completion and exits with a status code. In Kubernetes it is a Job
that must succeed before the new API version is released; locally it is a
compose service the API depends on.

Postgres identifiers are `snake_case` (via `EFCore.NamingConventions`),
because Postgres folds unquoted identifiers to lower case and PascalCase names
end up permanently quoted in every hand-written query.

Primary keys are UUIDv7, which gives a time-prefixed, index-friendly key that
can also serve as a keyset-pagination cursor. Note the limit: the ordering is
only chronological *across* milliseconds — within one millisecond the tail is
random. Paging stays correct (Postgres compares `uuid` byte-wise, a stable
total order); only the order inside a single millisecond is unspecified.

## Alternatives considered

| Option | Why not |
| --- | --- |
| Migrate at API startup | Races between replicas; couples schema timing to deploy timing; a slow migration restarts the pod |
| Migrate as an init container | Every replica races to migrate |
| Hand-written SQL migrations | More control, but the model and the schema drift apart silently; EF's model snapshot catches that |
| A shared schema for all modules | Permits cross-module joins, which destroys the cheap-extraction property of [0002](0002-modular-monolith-before-microservices.md) |
| `bigint` identity keys | Smaller and faster, but exposes row counts, and IDs cannot be generated client-side or before insert |

## Consequences

**Good**

- Schema changes are a reviewable, independently runnable, independently
  rollback-able step.
- Each module's schema can be migrated — and eventually split — on its own.
- Readable SQL in `psql`, and in every log line.

**Bad / accepted costs**

- The deploy pipeline has one more step, and its ordering is load-bearing.
- **Every migration must be backward compatible with the outgoing version**,
  because both versions serve traffic during a rolling deploy. Column drops
  and renames become two-release changes. This is the real cost, and it is
  documented in [the migrations runbook](../runbooks/database-migrations.md).
- UUID keys are 16 bytes and index less densely than `bigint`.

**What this makes harder later**

- Moving off EF Core would mean re-expressing the migration history.

## Revisit when

A module's write throughput outgrows a single Postgres primary, or a module's
data shape stops being relational. Either is a reason to give *that module* a
different store — not to change this decision wholesale.
