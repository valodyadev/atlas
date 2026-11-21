import Foundation

/// HTTP verbs the client supports.
public enum HTTPMethod: String, Sendable {
    case get = "GET"
    case post = "POST"
    case patch = "PATCH"
    case put = "PUT"
    case delete = "DELETE"

    /// Whether retrying the request is safe.
    ///
    /// Only idempotent verbs are retried automatically. Retrying a POST can
    /// create two records, which is exactly the bug that is impossible to
    /// reproduce and awful to clean up.
    public var isIdempotent: Bool {
        switch self {
        case .get, .put, .delete: true
        case .post, .patch: false
        }
    }
}

/// A single API operation, independent of how it is executed.
///
/// Endpoints are values, so they can be constructed in tests and compared
/// without a network stack.
public struct Endpoint: Sendable {
    public let method: HTTPMethod
    public let path: String
    public let query: [URLQueryItem]
    public let body: Data?
    public let requiresAuthentication: Bool

    public init(
        method: HTTPMethod = .get,
        path: String,
        query: [URLQueryItem] = [],
        body: Data? = nil,
        requiresAuthentication: Bool = true
    ) {
        self.method = method
        self.path = path
        self.query = query
        self.body = body
        self.requiresAuthentication = requiresAuthentication
    }

    /// Builds an endpoint whose body is a JSON-encoded value.
    public static func json(
        method: HTTPMethod,
        path: String,
        body: some Encodable & Sendable,
        requiresAuthentication: Bool = true
    ) throws -> Endpoint {
        try Endpoint(
            method: method,
            path: path,
            body: AtlasJSON.encoder.encode(body),
            requiresAuthentication: requiresAuthentication
        )
    }

    /// Resolves the endpoint against a base URL.
    func url(relativeTo baseURL: URL) throws(APIError) -> URL {
        let trimmed = path.hasPrefix("/") ? String(path.dropFirst()) : path

        guard var components = URLComponents(
            url: baseURL.appendingPathComponent(trimmed),
            resolvingAgainstBaseURL: true
        ) else {
            throw APIError.invalidURL(path: path)
        }

        if !query.isEmpty {
            components.queryItems = query
        }

        guard let url = components.url else {
            throw APIError.invalidURL(path: path)
        }

        return url
    }
}
