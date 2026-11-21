# iOS app

SwiftUI, Swift 6 language mode, iOS 18+. All the code lives in a local Swift
package; the app target is a shell.

## Layout

```
Packages/AtlasKit/        all the code
  Sources/
    AtlasCore             logging, telemetry contract, clock, environment
    AtlasNetworking       API client, error shape, retries, JSON coding
    AtlasDesignSystem     tokens, button styles, loading/empty/error views
    AtlasFeatureUsers     reference feature: repository, view model, views
  Tests/                  swift-testing suites, one per target

App/
  Sources/                entry point, composition root, config, Keychain
  Resources/              Info.plist, asset catalogue
  Config/*.xcconfig       build settings as reviewable text

project.yml               XcodeGen spec - the .xcodeproj is GENERATED
Tests/AtlasUITests/       a deliberately thin XCUITest suite
```

Dependencies between targets are declared in `Package.swift`, so a feature
importing another feature is a compile error.

## Working on it

```bash
make test-ios        # the whole unit suite, seconds, no Xcode needed
make ios-project     # generate Atlas.xcodeproj
open ios/Atlas.xcodeproj
```

Run `make ios-project` again whenever you pull a change that adds or removes
files. The project is generated and not committed — see
[ADR 0006](../docs/adr/0006-ios-swift-packages-and-generated-project.md) for
why.

Most work needs no simulator at all. The package targets declare `macOS`
alongside `iOS` specifically so `swift test` runs the unit suite on a bare
toolchain in seconds. Anything genuinely UIKit-only belongs in the app shell
or behind `#if os(iOS)`.

## Configurations

| Configuration | Bundle id | Backend |
| --- | --- | --- |
| Debug | `com.atlas.app.debug` | `http://localhost:8080/api/v1` |
| Staging | `com.atlas.app.staging` | `https://api.staging.example.com/api/v1` |
| Release | `com.atlas.app` | `https://api.example.com/api/v1` |

Three, not two: Staging is a **release-optimised** build pointing at the
staging backend, which is what a TestFlight build should be. A
Debug-optimised build does not show you the optimiser behaviour or the
performance your users will get.

Values flow `.xcconfig` → `Info.plist` → `AppConfiguration`, so environments
differ by build setting rather than by `#if` branches scattered through the
source — and a TestFlight build can point at staging without a code change.

`AppConfiguration.fromBundle()` is deliberately fatal on bad input: an app
that launches with no API URL can only fail later, confusingly, in front of a
user.

### Running against a physical device

The simulator reaches your Mac's `localhost`; a device does not. Point
`ATLAS_API_BASE_URL` in `Debug.xcconfig` at your machine's LAN address. The
`NSAllowsLocalNetworking` exception in `Info.plist` already permits plain HTTP
to local addresses — and nothing else, which is why it is used instead of
`NSAllowsArbitraryLoads`.

## Conventions

- **State**: `@Observable` + `@MainActor` view models. No `@Published`, no
  Combine.
- **Views render state, they do not fetch it.** A view model exposes a
  `ViewState` enum and the view switches over it — including distinct `empty`
  and `failed` cases. Conflating `empty` with `loaded([])` is how you ship a
  blank screen.
- **Features depend on protocols.** `UsersListViewModel` takes a
  `UsersRepository`, so its tests are a seven-line stub rather than a URL
  protocol mock.
- **Design tokens, never literals.** `Atlas.Spacing.lg`, not `16`.
- **Dynamic Type everywhere.** Styles derive from text styles, never fixed
  point sizes. This is an accessibility requirement, not a preference.
- **No third-party dependencies** by default —
  [ADR 0007](../docs/adr/0007-ios-dependency-policy.md) explains the policy
  and lists what is pre-vetted if you need it.
- **`AtlasLog`, never `print`.** SwiftLint enforces this: `print` output is
  not captured on a user's device.

## Testing

`swift-testing` (`@Test`, `#expect`) for unit tests; XCUITest for the handful
of UI journeys, because UI automation still runs on XCTest.

The UI suite is intentionally thin. UI tests are the slowest and flakiest
tier, so they cover launch and navigation only; everything else is a view
model test. Growing `AtlasUITests` is usually a sign a view model test is
missing.

The API client is tested through a `URLProtocol` stub, so the real
`URLSession` path — headers, status mapping, retries, decoding — is genuinely
exercised rather than mocked away.

## Known gaps

- **No crash reporting.** First dependency to add; conform a vendor SDK to the
  existing `Telemetry` protocol and nothing else changes.
- **`KeychainTokenStore.refreshToken()` returns `nil`.** Wire it to your
  identity provider's token endpoint. Until then the client surfaces
  `.unauthorized`, which is the correct behaviour.
- **Models are hand-written** from the OpenAPI contract, not generated. See
  [ADR 0008](../docs/adr/0008-api-contract-sharing.md) for when to switch.
