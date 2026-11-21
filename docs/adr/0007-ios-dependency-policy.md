# 0007. First-party iOS dependencies by default

- **Status:** Accepted
- **Date:** 2026-11-21

## Context

The brief asked for current iOS technologies and established libraries. On
iOS in 2026 those two phrases point in a particular direction: much of what
third-party libraries were needed for has moved into the platform.

- Networking: `URLSession` with `async`/`await` covers what Alamofire was for.
- State: `@Observable` (Observation) replaces the `@Published` boilerplate.
- Persistence: SwiftData covers what a Core Data wrapper was for.
- Testing: swift-testing ships with the toolchain.
- Concurrency: structured concurrency and the Swift 6 language mode replace
  reactive frameworks for most coordination.

Every dependency added to `AtlasKit` is inherited by every engineer on the
project: a version to track, a migration when Swift changes, a supply-chain
surface, and a constraint on the minimum deployment target.

## Decision

`AtlasKit` declares **zero third-party dependencies**. The stack is SwiftUI,
Observation, structured concurrency, `URLSession`, OSLog, SwiftData and
swift-testing.

Developer tools (SwiftFormat, SwiftLint, XcodeGen) are installed as CLI
binaries via Homebrew and invoked by `make` and CI — not as SPM build plugins,
which would pin versions into the build graph and require network access at
build time.

Adding a dependency is allowed, and needs an ADR answering: what platform API
is insufficient, what the migration path off it is, and who updates it when
Swift N+1 breaks it.

Pre-vetted, if the need arises:

| Need | Library |
| --- | --- |
| Crash and error reporting | Sentry, Datadog — behind the existing `Telemetry` protocol |
| Dependency injection, if the composition root stops scaling | swift-dependencies |
| Snapshot testing | swift-snapshot-testing |
| Keychain ergonomics | Only if `KeychainTokenStore` grows beyond one token |

## Alternatives considered

| Option | Why not |
| --- | --- |
| Add the usual suspects up front | Pays the cost before the need; most would be unused |
| Ban third-party code outright | Would mean hand-rolling crash reporting, which is not a good use of anyone's time |

## Consequences

**Good**

- `swift build` works offline, from a clean checkout, with no resolution step.
- Nothing to migrate when the Swift toolchain moves.
- The supply-chain surface of the app is Apple's.

**Bad / accepted costs**

- Some wheels get reinvented: the retry policy, the `Telemetry` protocol and
  the Keychain wrapper are all small amounts of code a library would have
  provided.
- Crash reporting is **not** wired up. `OSLogTelemetry` writes to the device
  log, which is not a substitute. This is the first dependency to add.

**What this makes harder later**

- Nothing — adding a dependency stays a one-line change in `Package.swift`.
  The policy is a default, not a lock.

## Revisit when

Crash reporting is needed in a real build (soon), or the composition root in
`AppDependencies` stops being readable.
