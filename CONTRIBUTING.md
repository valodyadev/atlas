# Contributing

## Setup

```bash
make bootstrap
make doctor
make install-hooks
```

## Workflow

1. Branch from `main`.
2. Make the change, with tests that fail without it.
3. `make lint && make test`.
4. Open a pull request; fill in the template honestly — especially the rollout
   and rollback section.

## Rules that are enforced, not suggested

These fail CI, so finding out early is cheaper:

- **Module boundaries.** A module referencing a sibling, domain code importing
  EF Core, or the API host referencing a data provider all fail
  `Atlas.Architecture.Tests`. If you believe a rule is wrong, change it in an
  ADR and in the test — not around it.
- **Formatting.** `dotnet format`, SwiftFormat, Prettier and `ruff format` all
  run in `--check` mode. `make format` fixes everything they can.
- **Warnings are errors.** In C#, in Swift (`-warnings-as-errors`), and in the
  app target. A warning nobody fixes is a warning everybody scrolls past.
- **The API contract.** Change an endpoint or a DTO, run `make openapi`, commit
  the result.
- **No secrets.** gitleaks scans the full history on every push.

## New module

Follow [adding a module](docs/architecture.md#adding-a-module). `Identity` is
the reference implementation; copy its shape rather than inventing a new one.

## New service in another runtime

Read the admission criteria in [`services/README.md`](services/README.md)
first. The bar is a forcing reason, not a preference, and the seven
operational rules are not optional.

## Architectural decisions

Anything hard to reverse, anything that constrains how other code must be
written, and anything a newcomer will question gets an ADR. Copy
[`docs/adr/template.md`](docs/adr/template.md). An ADR with no downsides
listed has not been thought through — the **Consequences** section is the
point.

ADRs are immutable once accepted: supersede, do not edit.

## Database changes

Read [the migrations runbook](docs/runbooks/database-migrations.md) before
writing one. The short version: during a rolling deploy the **old** code runs
against the **new** schema, so a migration must be backward compatible with
the version currently deployed. Drops and renames are two- or three-release
changes.

Always read the generated SQL (`make migration-sql MODULE=...`). EF will
happily turn an innocent model edit into a column narrowing.

## What review looks for

In rough order:

1. **Does it work, and is that demonstrated by a test that fails without it?**
2. **Does it respect the boundaries?** Not just the enforced ones — would this
   still be extractable as a service?
3. **What happens when it fails?** Is the failure visible in telemetry,
   attributable to a module, and actionable by whoever is on call?
4. **Can the next person understand it?** Comments should explain *why*, not
   *what*. The code already says what.
5. **Does it leak anything?** Credentials, personal data, internal exception
   messages in a 5xx response.

Formatting and style are not reviewed, because the tools own them.

## Commit messages

Imperative subject under 72 characters, then a body explaining *why* if it is
not obvious. The diff shows what changed; the message exists for whoever is
reading `git log` in two years trying to work out what you were thinking.
