import Foundation

/// Everything that can go wrong on the way to a decoded response.
///
/// Exhaustive on purpose: a `switch` over this is how a feature decides
/// between "show a message", "ask the user to sign in again" and "offer retry".
public enum APIError: Error, Sendable {
    /// The endpoint could not be turned into a URL.
    case invalidURL(path: String)

    /// The request never completed (offline, DNS, timeout).
    case transport(underlying: any Error)

    /// The server answered with a non-success status.
    case server(status: Int, problem: ProblemDetails?)

    /// Authentication is required or no longer valid.
    case unauthorized(problem: ProblemDetails?)

    /// The caller is rate limited.
    case rateLimited(retryAfter: TimeInterval?)

    /// The body did not match the expected shape.
    case decoding(underlying: any Error)

    /// The request was cancelled.
    case cancelled

    /// A stable, dotted error code from the server, when one was supplied.
    public var code: String? {
        switch self {
        case let .server(_, problem), let .unauthorized(problem): problem?.code
        default: nil
        }
    }

    /// Whether retrying this request unchanged could plausibly succeed.
    public var isRetryable: Bool {
        switch self {
        case .transport: true
        case .rateLimited: true
        case let .server(status, _): status >= 500
        case .invalidURL, .unauthorized, .decoding, .cancelled: false
        }
    }
}

extension APIError: LocalizedError {
    public var errorDescription: String? {
        switch self {
        case let .invalidURL(path):
            "Could not build a request for \(path)."
        case .transport:
            "The network is unavailable. Check your connection and try again."
        case let .server(status, problem):
            problem?.detail ?? "The server returned an error (\(status))."
        case .unauthorized:
            "Your session has expired. Please sign in again."
        case .rateLimited:
            "Too many requests. Please wait a moment and try again."
        case .decoding:
            "The server sent an unexpected response."
        case .cancelled:
            "The request was cancelled."
        }
    }
}
