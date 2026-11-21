import AtlasCore
import Foundation

/// Where the API lives and how long we are willing to wait for it.
public struct APIConfiguration: Sendable {
    /// Base URL including the version prefix, e.g. `https://api.example.com/api/v1`.
    public let baseURL: URL

    /// Per-request timeout.
    public let timeout: TimeInterval

    /// Ceiling on a whole attempt, including any wait for connectivity.
    /// Without this the session would wait for days rather than fail.
    public let resourceTimeout: TimeInterval

    /// Which deployment this points at.
    public let environment: AppEnvironmentKind

    public init(
        baseURL: URL,
        environment: AppEnvironmentKind,
        timeout: TimeInterval = 30,
        resourceTimeout: TimeInterval = 60
    ) {
        self.baseURL = baseURL
        self.environment = environment
        self.timeout = timeout
        self.resourceTimeout = resourceTimeout
    }
}
