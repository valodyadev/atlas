import Foundation

/// Time as a dependency, so time-dependent behaviour is testable.
public protocol DateProviding: Sendable {
    /// The current instant.
    var now: Date { get }
}

/// Reads the system clock.
public struct SystemDateProvider: DateProviding {
    public init() {}
    public var now: Date { Date() }
}

/// Returns a fixed instant. For tests and previews.
public struct FixedDateProvider: DateProviding {
    public let now: Date
    public init(now: Date) { self.now = now }
}
