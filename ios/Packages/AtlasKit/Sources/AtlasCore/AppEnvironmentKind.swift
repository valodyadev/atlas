import Foundation

/// Which backend deployment this build talks to.
///
/// Resolved once at launch from the build configuration (see the app's
/// `.xcconfig` files) rather than from a compile-time `#if DEBUG`, so a
/// TestFlight build can point at staging without a separate source branch.
public enum AppEnvironmentKind: String, Sendable, CaseIterable {
    case development
    case staging
    case production

    /// Whether diagnostic surfaces (verbose logs, debug menu) are allowed.
    public var allowsDiagnostics: Bool {
        self != .production
    }
}
