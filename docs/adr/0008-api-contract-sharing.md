# 0008. Committed OpenAPI contract, hand-written client for now

- **Status:** Accepted
- **Date:** 2026-11-21

## Context

The iOS app and the API are released on completely different cadences. The
backend can deploy hourly; an app already on a user's phone cannot be forced
to update. So the contract between them is the most important interface in the
system, and the failure mode is specific and nasty: a field is renamed, nobody
notices, and the next App Store release crashes on a decoding error for users
who are three versions behind.

## Decision

Three parts.

**1. The contract is generated from the code and committed.**
`Microsoft.AspNetCore.OpenApi` produces the document from the endpoints
themselves, so it cannot drift from the implementation.
[`tools/scripts/export-openapi.sh`](../../tools/scripts/export-openapi.sh)
writes it to `contracts/openapi/v1.json`, and CI fails if the committed file
differs from what the code produces. An API change therefore shows up as a
reviewable diff in a pull request.

**2. The API is versioned from day one.** `Asp.Versioning` with URL-segment
versioning (`/api/v1/...`). Retrofitting versioning after the first App Store
release is a migration project; adding it now costs one extension method. The
`v{version}` placeholder is substituted with the concrete version in the
generated document, so client generators do not emit a spurious `version`
parameter.

**3. The Swift models are hand-written for now.** `AtlasFeatureUsers` declares
`User`, `UserPage` and `CreateUserRequest` by hand, mirroring the C# records.

That third part is the deliberate compromise. Generating them with
`swift-openapi-generator` is the better end state and the explicit plan — but
it adds an SPM build plugin and a build-time code-generation step, which
contradicts [0007](0007-ios-dependency-policy.md) while the API surface is
three endpoints. Hand-written models are honest about the current size.

The risk is that a hand-written model silently diverges. That is mitigated,
not eliminated: the contract diff in CI makes any API change visible to
whoever reviews the pull request, and `AtlasJSON` tests decode the exact date
formats ASP.NET Core emits.

## Alternatives considered

| Option | Why not |
| --- | --- |
| Generate the Swift client now | Correct eventually; premature for three endpoints, and adds a build plugin |
| Hand-write the OpenAPI document | Drifts from the code immediately; defeats the purpose |
| No committed contract, read it from a running service | Nothing to diff in review, so breaking changes are invisible until runtime |
| Share types via a cross-platform IDL (protobuf, Smithy) | Strong option for a larger estate; a second toolchain and a second source of truth for a REST API this size |

## Consequences

**Good**

- Breaking changes are visible in review, as a diff.
- The contract is a build artefact, so it is always accurate.
- Versioned URLs mean two app versions can be supported at once.

**Bad / accepted costs**

- **Swift models must be updated by hand when the contract changes, and
  nothing enforces it.** This is the known weak point of this decision.
- `contracts/openapi/v1.json` shows up in diffs on most API pull requests.

**What this makes harder later**

- Nothing: moving to a generated client replaces the hand-written models and
  leaves the rest of the decision intact.

## Revisit when

Any of: the API passes roughly ten endpoints; a second client appears (web,
Android, a partner); or a hand-written model diverges from the contract in a
way that reaches a user. The first of those is likely to come first — switch
to `swift-openapi-generator` then.
