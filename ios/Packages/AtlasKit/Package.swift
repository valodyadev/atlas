// swift-tools-version:6.1
import PackageDescription

// ---------------------------------------------------------------------------
// AtlasKit - the app's code, as a local Swift package.
//
// Why a package rather than Xcode targets:
//   * `swift build` / `swift test` run in CI without Xcode or a simulator,
//     so the unit suite is seconds rather than minutes;
//   * dependencies between layers are declared and enforced by the compiler,
//     so a feature cannot quietly reach into another feature;
//   * the app target shrinks to a shell, which keeps incremental builds fast
//     as the number of features grows.
//
// Targets also declare macOS so the whole package compiles and tests on a
// plain Swift toolchain. Anything genuinely UIKit-only belongs in the app
// shell or behind `#if os(iOS)`.
// ---------------------------------------------------------------------------

let strictConcurrency: [SwiftSetting] = [
    // Swift 6 language mode: data races are compile errors, not crash reports.
    .swiftLanguageMode(.v6),
]

let package = Package(
    name: "AtlasKit",
    platforms: [
        .iOS(.v18),
        .macOS(.v14),
    ],
    products: [
        .library(name: "AtlasCore", targets: ["AtlasCore"]),
        .library(name: "AtlasNetworking", targets: ["AtlasNetworking"]),
        .library(name: "AtlasDesignSystem", targets: ["AtlasDesignSystem"]),
        .library(name: "AtlasFeatureUsers", targets: ["AtlasFeatureUsers"]),
    ],
    dependencies: [
        // Intentionally empty. See docs/adr/0007-ios-dependency-policy.md:
        // the current stack (SwiftUI, Observation, async/await, URLSession,
        // SwiftData, swift-testing) is first-party, and every third-party
        // dependency added here is one the whole team inherits.
    ],
    targets: [
        // --- Layer 1: foundation -------------------------------------------
        .target(
            name: "AtlasCore",
            swiftSettings: strictConcurrency
        ),

        // --- Layer 2: capabilities -----------------------------------------
        .target(
            name: "AtlasNetworking",
            dependencies: ["AtlasCore"],
            swiftSettings: strictConcurrency
        ),
        .target(
            name: "AtlasDesignSystem",
            dependencies: ["AtlasCore"],
            swiftSettings: strictConcurrency
        ),

        // --- Layer 3: features ---------------------------------------------
        // One target per feature. Features depend on layers 1-2 and never on
        // each other; shared behaviour is promoted into a lower layer.
        .target(
            name: "AtlasFeatureUsers",
            dependencies: ["AtlasCore", "AtlasNetworking", "AtlasDesignSystem"],
            swiftSettings: strictConcurrency
        ),

        // --- Tests ----------------------------------------------------------
        .testTarget(
            name: "AtlasCoreTests",
            dependencies: ["AtlasCore"],
            swiftSettings: strictConcurrency
        ),
        .testTarget(
            name: "AtlasNetworkingTests",
            dependencies: ["AtlasNetworking"],
            swiftSettings: strictConcurrency
        ),
        .testTarget(
            name: "AtlasFeatureUsersTests",
            dependencies: ["AtlasFeatureUsers"],
            swiftSettings: strictConcurrency
        ),
    ]
)
