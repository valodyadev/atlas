import Foundation

/// How many times and how long to wait before retrying.
public struct RetryPolicy: Sendable {
    public let maxAttempts: Int
    public let baseDelay: Duration
    public let maxDelay: Duration

    public init(maxAttempts: Int = 3, baseDelay: Duration = .milliseconds(250), maxDelay: Duration = .seconds(5)) {
        self.maxAttempts = max(1, maxAttempts)
        self.baseDelay = baseDelay
        self.maxDelay = maxDelay
    }

    /// No retries. Useful in tests, where waiting is just slowness.
    public static let none = RetryPolicy(maxAttempts: 1)

    /// Exponential backoff with full jitter.
    ///
    /// Jitter matters more than the backoff curve: without it, every device
    /// that failed during an outage retries in lockstep and knocks the service
    /// over again the moment it recovers.
    public func delay(forAttempt attempt: Int) -> Duration {
        let exponent = max(0, attempt - 1)
        let scaled = baseDelay * (1 << min(exponent, 10))
        let capped = min(scaled, maxDelay)
        let jitterFraction = Double.random(in: 0.5 ... 1.0)
        return capped * jitterFraction
    }
}
