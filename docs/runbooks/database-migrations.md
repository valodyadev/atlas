# Database migrations

## The one rule that matters

**Every migration must be backward compatible with the version currently
deployed.**

During a rolling deploy both versions serve traffic at the same time, against
the same schema. The migration runs *before* the new version is released, so
for a window of minutes the **old** code is running against the **new**
schema. A migration that the old code cannot tolerate takes production down,
and it does so after the point where rolling back the code alone would help.

This is the entire reason migrations are a separate job
([ADR 0004](../adr/0004-postgresql-with-migrations-as-a-job.md)) rather than a
startup step.

## Safe and unsafe changes

| Change | Safe in one release? | Notes |
| --- | --- | --- |
| Add a nullable column | Yes | |
| Add a column with a default | Yes, on Postgres 11+ | No table rewrite |
| Add a table | Yes | |
| Add an index | Yes, with `CONCURRENTLY` | A plain `CREATE INDEX` takes a write lock for the duration |
| Widen a column | Usually | `varchar(n)` → `varchar(m>n)` or `text` is metadata-only |
| **Drop a column** | **No** | Old code still selects it |
| **Rename a column** | **No** | A rename is a drop plus an add |
| **Add a `NOT NULL` column without a default** | **No** | Old code's inserts omit it |
| **Narrow a column or tighten a constraint** | **No** | Old code can still write a now-invalid value |
| **Change a column's type** | **No** | |

## The expand/contract pattern

Any "no" above becomes two or three releases.

Renaming `users.display_name` to `users.full_name`:

**Release 1 — expand.** Add `full_name` as nullable. Deploy code that writes
*both* columns and reads `full_name` with a fallback to `display_name`.

**Release 2 — backfill and switch.** Backfill `full_name` in batches (see
below). Deploy code that reads and writes only `full_name`.

**Release 3 — contract.** Drop `display_name`.

Tedious, and it is what makes a rename a non-event instead of an incident.

## Backfilling

Never `UPDATE users SET ...` across a large table in a migration: it takes a
long lock, holds a huge transaction, and blocks the deploy behind it.

Batch it, and keep it out of the migration:

```sql
UPDATE identity.users
SET full_name = display_name
WHERE id IN (
  SELECT id FROM identity.users
  WHERE full_name IS NULL
  ORDER BY id
  LIMIT 1000
);
```

Run it from a one-off job or a maintenance script until it reports zero rows.
The migration itself should only ever change the schema.

## Day to day

```bash
make migration NAME=AddOrdersTable MODULE=Identity   # create
make migration-sql MODULE=Identity                   # review the SQL before you commit
make migrate                                         # apply locally
```

**Always read the generated SQL.** EF will cheerfully produce a destructive
migration from an innocent-looking model change — a changed `MaxLength` can
become a column narrowing, and a renamed property becomes a drop plus an add.

## Deployment order

```
1. build images
2. run the db-migrator Job   ──▶ must exit 0
3. roll the API Deployment
```

If step 2 fails, step 3 never runs and the previous version keeps serving. The
Job keeps finished pods for an hour (`ttlSecondsAfterFinished`) so its logs
are retrievable after a failed deploy.

## When a migration fails in production

1. **Do not re-run it blindly.** Read the Job's logs first:
   `kubectl logs job/atlas-db-migrator-<id>`.
2. EF runs each migration in a transaction, so a failed migration is rolled
   back and the history table is unchanged — the schema is as it was. The
   exceptions are statements Postgres cannot run transactionally, notably
   `CREATE INDEX CONCURRENTLY`; if your migration contains one, verify the
   actual state before retrying.
3. The API has not rolled, so the old version is still serving. There is no
   user impact yet. Resist the urge to hurry.
4. Fix forward: a new migration that corrects the problem. Rolling back a
   migration against a database that already took writes under the new schema
   loses data.
5. If the schema is genuinely inconsistent, restore from a point-in-time
   backup. Which is worth having rehearsed before you need it.

## Checklist before merging a migration

- [ ] Read the generated SQL (`make migration-sql`)
- [ ] It is backward compatible with the deployed version, or it is release 1
      of an expand/contract sequence
- [ ] No index created without `CONCURRENTLY` on a table with meaningful volume
- [ ] No data backfill inside the migration
- [ ] Rehearsed against a realistic copy, not just an empty local database
- [ ] You know how long it takes on production-sized data
