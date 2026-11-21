import Foundation

/// A product analytics / crash reporting sink.
///
/// The app depends on this protocol, never on a vendor SDK. Swapping or adding
/// a vendor is then a single conforming type in the app shell, and unit tests
/// assert on a recorded array instead of reaching the network.
public protocol Telemetry: Sendable {
    /// Records a product event.
    /// - Parameters:
    ///   - name: Stable, dotted event name, e.g. `users.list.viewed`.
    ///   - metadata: Non-identifying context.
    func record(event name: String, metadata: [String: String])

    /// Records a handled error.
    /// - Parameters:
    ///   - error: The error.
    ///   - context: Where it happened.
    func record(error: Error, context: String)
}

public extension Telemetry {
    /// Records a product event with no metadata.
    /// - Parameter name: Stable, dotted event name.
    func record(event name: String) {
        record(event: name, metadata: [:])
    }
}

/// Writes telemetry to the system log. The default in development.
public struct OSLogTelemetry: Telemetry {
    public init() {}

    public func record(event name: String, metadata: [String: String]) {
        AtlasLog.app.info("event \(name, privacy: .public) \(metadata.description, privacy: .private)")
    }

    public func record(error: Error, context: String) {
        AtlasLog.app.error("error in \(context, privacy: .public): \(error.localizedDescription, privacy: .private)")
    }
}

/// Discards telemetry. The default in tests and previews.
public struct NoopTelemetry: Telemetry {
    public init() {}
    public func record(event name: String, metadata: [String: String]) {}
    public func record(error: Error, context: String) {}
}
