# Open items

Risks, questions and deferred work that would otherwise live only in a chat
transcript. Append; do not silently delete. When an item closes, move it to
**Closed** with the date and what resolved it.

Updated 2026-11-21.

## Risks

| # | Risk | Impact | Current position |
| --- | --- | --- | --- |
| R1 | GitHub Actions is blocked by an account billing lock. No workflow has ever executed. | All five workflow files are unverified end to end. CI cannot be used as evidence of anything. | Local gates only. Clear the lock at github.com/settings/billing, then re-run. |
| R3 | No identity provider is wired. `Auth:Enabled=false` in Development; the API refuses to start with it off anywhere else. | The app has no real authentication. `KeychainTokenStore.refreshToken()` returns nil. | Deliberate. Point `Auth:Authority` at an issuer and implement the refresh. |
| R4 | No crash reporting on iOS. `OSLogTelemetry` writes to the device log, which is not a substitute. | Production crashes are invisible. | First third-party dependency to add; conform a vendor SDK to the existing `Telemetry` protocol (ADR 0007). |
| R5 | Swift models are hand-written from the OpenAPI contract, not generated. | A field rename can diverge silently and break a shipped app. | Known weakness of ADR 0008. The contract-drift check in CI makes API changes visible in review. Switch to `swift-openapi-generator` at roughly ten endpoints. |
| R6 | Domain events are raised but never dispatched. `AggregateRoot.Raise` collects them; `UserService` clears them. | Anything relying on them silently does nothing. | Wire a dispatcher, ideally with a transactional outbox, before depending on them. |
| R7 | Deployment is a stub. `release.yml` builds, signs and attests images; the deploy job only prints. | No automated path to an environment. | Needs cluster and credential decisions. Use OIDC federation, not a stored key. |
| R11 | macOS 27 is a pre-release release that Homebrew treats as a Tier 2 configuration. `brew install swiftlint` fails outright with `Error: unknown or unsupported macOS version: :dunno`. | Any formula without a matching bottle may fail to install. SwiftLint had to come from the official `portable_swiftlint.zip` release instead. | Accepted. Use upstream binaries when a formula refuses. Revisit when macOS 27 ships and Homebrew promotes it. |
| R12 | Local toolchain is Xcode 27.2 / Swift 6.4.2; CI now pins `Xcode_26.6` on `macos-26`. | Narrowed on 2026-11-21 from `Xcode_16.4` on `macos-15`, which was two Xcode generations behind. A residual gap remains and cannot be closed: GitHub's runner images do not offer Xcode 27 at all (`macos-15` tops out at 26.3, `macos-26` at 26.6), so local will stay ahead of CI. | Partly mitigated. The pin is deliberately hard, so if the image drops 26.6 the job fails loudly rather than silently using another toolchain; both jobs now print `xcodebuild -version` and `swift --version`. Still unexercised, because CI cannot run at all (R1). |
| R14 | Simulator device names are not stable across runner images. `iPhone 16` does not exist on `macos-26`, which carries iPhone 16e, 17, 17 Pro, 17 Pro Max and 17e. The destination was updated to `iPhone 17`. | A stale device name fails the `app` job with an unhelpful destination error rather than a build error. | Check the runner image manifest whenever the image or Xcode pin moves. |
| R13 | Xcode 27 downloads simulator runtimes separately and none is installed. `xcrun simctl list runtimes` is empty. | The app target cannot be built or run, so `ios.yml`'s `app` job and the two XCUITests are unverified. The 38 package tests do not need a simulator and are unaffected. | Install a runtime from Xcode > Settings > Components. |
| R9 | The DbMigrator logs `Error: libgssapi_krb5.so.2: cannot open shared object file` on every run. Npgsql probes for Kerberos/GSSAPI support, and the chiseled base image has no krb5 library. | None functionally — the job still exits 0 and applies migrations, because we authenticate with a password. But an error-level line in a deploy job's log that is actually benign trains people to ignore deploy logs. | Accepted for now. Chiseled images have no package manager, so the fix is either a base image that ships krb5 or an Npgsql setting to skip the probe. Neither verified. Would become a real failure if Kerberos auth were ever configured. |
| R10 | `createdAt` comes back with 7 fractional digits from a `POST` response and 6 from a subsequent `GET`. Postgres `timestamptz` has microsecond precision, so the 7th digit the .NET `DateTimeOffset` carried in memory is truncated on write. | A client that caches the create response and later compares `createdAt` for equality against a read will see a mismatch. Observed live: `...4613889+00:00` on create, `...461388+00:00` on read. | Inherent to the column type, not a defect. Clients must not compare timestamps for exact equality across a write/read boundary. `AtlasJSON` already decodes both widths. |
| R8 | Agent path ownership is convention, not a sandbox. Frontmatter restricts tools, not paths. | Two agents could still edit the same file if briefed carelessly. | One owner per path in `CLAUDE.md`; read-only `atlas-qa`; `isolation: "worktree"` for hard cases. |

## Open questions

| # | Question | Blocks | Asked |
| --- | --- | --- | --- |
| Q1 | Close the four stale Dependabot PRs (#1–#4), or let the weekly docker run close them? | Nothing. They are harmless while unmerged; #1 and #2 would break the build if merged. | 2026-11-21 — needs `gh auth login`, which only you can do |
| Q2 | Will this repo move into a GitHub organisation? | `CODEOWNERS` currently assigns everything to `@valodyadev`; team syntax needs an org. | 2026-11-21 |
| Q3 | Which cloud and cluster is the deploy target? | `infra/terraform/` is an empty placeholder; the Kustomize overlays assume a cluster exists. | 2026-11-21 |

## Deferred

| # | Work | Why deferred |
| --- | --- | --- |
| D1 | Five bug fixes from the container-runtime verification pass are **uncommitted** in the working tree, plus an untracked `.dockerignore`. | You asked not to commit. See the handoff note below. |
| D2 | `swift-openapi-generator` for the iOS client. | Premature at three endpoints (ADR 0008). Revisit at ~10, or when a second client appears. |
| D3 | Terraform for cluster, database and secret store. | Depends on Q3. |
| D4 | Crash reporting on iOS. | See R4. |

## Handoff note — 2026-11-21

Done and verified: the starter repo is pushed (`1e56ac4` on `main`). The
backend suite passes 35/35 with nothing skipped against a real PostgreSQL 18
via Colima. All four container images build; the Node and Python containers
were confirmed serving `/health/live`. iOS 38, Node 9, Python 13 all green,
`make lint` clean.

Done but **not** committed: five fixes found by actually running things —
the root `.dockerignore`, the Postgres 18 volume path, the DbMigrator base
image, `AddServiceDefaults()` in the migrator plus a `TryAdd` baseline in
`AddModules`, the Colima socket override in `ContainerRuntime`, and the
SwiftLint custom-rule false positive.

Verified 2026-11-21, after the five fixes: `make up` brings all seven
containers up. `postgres` reports healthy, `db-migrator` exits 0 with "All
migrations applied", and the API answers `/health/live`, `/health/ready` and
`/api/v1/meta` with 200. End to end through the real stack: `POST /api/v1/users`
returns 201 with a UUIDv7 id, `GET` round-trips it, and a duplicate returns 409
with `identity.user.email_taken` — so the unique index and the schema-per-module
layout work against a volume-backed Postgres 18. The `identity` schema holds
`users` and `__ef_migrations_history` as designed.

Next step: commit the five fixes. Nothing is blocking them.

## Closed

| # | Item | Resolved |
| --- | --- | --- |
| R2 | SwiftLint could not run (no full Xcode, so no `sourcekitdInProc.framework`). | 2026-11-21. Xcode 27.2 installed and selected. `swiftlint lint --strict` then found exactly one real violation — a 134-character line in `APIClient.swift`, over the 120 limit, which `ios.yml` would have failed on. Fixed by dropping a redundant `privacy: .public` from an integer interpolation (OSLog treats numerics as public by default), bringing it to 116. Now reports 0 violations in 26 files. Note `brew install swiftlint` does not work here — see R11. |
