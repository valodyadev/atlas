# Architecture decision records

One file per decision, numbered, append-only. A decision that turned out
wrong gets a new ADR that supersedes the old one; the old one stays, because
the reasoning that led there is the most useful thing in the file.

| # | Decision | Status |
| --- | --- | --- |
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0002](0002-modular-monolith-before-microservices.md) | Modular monolith before microservices | Accepted |
| [0003](0003-dotnet-primary-backend-with-polyglot-services.md) | .NET as the primary backend, with room for other runtimes | Accepted |
| [0004](0004-postgresql-with-migrations-as-a-job.md) | PostgreSQL, with migrations applied by a job | Accepted |
| [0005](0005-opentelemetry-over-otlp.md) | OpenTelemetry over OTLP, no vendor SDK in app code | Accepted |
| [0006](0006-ios-swift-packages-and-generated-project.md) | iOS modularised as Swift packages, project generated | Accepted |
| [0007](0007-ios-dependency-policy.md) | First-party iOS dependencies by default | Accepted |
| [0008](0008-api-contract-sharing.md) | Committed OpenAPI contract, hand-written client for now | Accepted |
| [0009](0009-argon2id-for-password-hashing.md) | Argon2id for password hashing | Accepted |
| [0010](0010-single-role-with-policy-authorization.md) | One role per user, authorised through policies | Accepted |
| [0011](0011-symmetric-signing-for-access-tokens.md) | Symmetric signing for access tokens, for now | Accepted |

## Writing one

Copy [`template.md`](template.md). Keep it to one page. The sections that
matter are **Context** (what forced a decision) and **Consequences** (what
this costs, including what it makes harder) — an ADR with no downsides listed
has not been thought through.
