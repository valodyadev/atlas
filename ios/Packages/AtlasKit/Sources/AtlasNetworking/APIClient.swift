import AtlasCore
import Foundation

/// Performs API requests.
///
/// Features depend on this protocol, never on `URLSession`, which is what lets
/// a view model be tested with a hand-written stub and no network at all.
public protocol APIClient: Sendable {
    /// Sends a request and decodes the response body.
    func send<Response: Decodable & Sendable>(
        _ endpoint: Endpoint,
        as responseType: Response.Type
    ) async throws(APIError) -> Response

    /// Sends a request that returns no body.
    func send(_ endpoint: Endpoint) async throws(APIError)
}

/// `URLSession`-backed client with auth, retries and problem+json decoding.
public actor URLSessionAPIClient: APIClient {
    private let configuration: APIConfiguration
    private let session: URLSession
    private let tokenProvider: any TokenProviding
    private let retryPolicy: RetryPolicy
    private let telemetry: any Telemetry

    public init(
        configuration: APIConfiguration,
        tokenProvider: any TokenProviding = AnonymousTokenProvider(),
        retryPolicy: RetryPolicy = RetryPolicy(),
        telemetry: any Telemetry = NoopTelemetry(),
        session: URLSession? = nil
    ) {
        self.configuration = configuration
        self.tokenProvider = tokenProvider
        self.retryPolicy = retryPolicy
        self.telemetry = telemetry

        if let session {
            self.session = session
        } else {
            let sessionConfiguration = URLSessionConfiguration.default
            sessionConfiguration.timeoutIntervalForRequest = configuration.timeout
            sessionConfiguration.waitsForConnectivity = true
            // The API is the source of truth; a stale cached response is worse
            // than a visible loading state.
            sessionConfiguration.requestCachePolicy = .reloadIgnoringLocalCacheData
            self.session = URLSession(configuration: sessionConfiguration)
        }
    }

    public func send<Response: Decodable & Sendable>(
        _ endpoint: Endpoint,
        as responseType: Response.Type
    ) async throws(APIError) -> Response {
        let data = try await perform(endpoint)

        do {
            return try AtlasJSON.decoder.decode(Response.self, from: data)
        } catch {
            telemetry.record(error: error, context: "decode \(endpoint.path)")
            throw APIError.decoding(underlying: error)
        }
    }

    public func send(_ endpoint: Endpoint) async throws(APIError) {
        _ = try await perform(endpoint)
    }

    // MARK: - Request pipeline

    private func perform(_ endpoint: Endpoint) async throws(APIError) -> Data {
        var lastError: APIError = .cancelled
        var didRefreshToken = false

        for attempt in 1 ... retryPolicy.maxAttempts {
            do {
                return try await attemptOnce(endpoint)
            } catch {
                lastError = error

                // A 401 earns exactly one refresh-and-retry. More than that
                // and a revoked token turns into a retry storm.
                if case .unauthorized = error, !didRefreshToken {
                    didRefreshToken = true
                    if await tokenProvider.refreshToken() != nil {
                        continue
                    }
                    throw error
                }

                let canRetry = error.isRetryable
                    && endpoint.method.isIdempotent
                    && attempt < retryPolicy.maxAttempts

                guard canRetry else { throw error }

                try? await Task.sleep(for: retryPolicy.delay(forAttempt: attempt))

                if Task.isCancelled {
                    throw APIError.cancelled
                }
            }
        }

        throw lastError
    }

    private func attemptOnce(_ endpoint: Endpoint) async throws(APIError) -> Data {
        var request = try URLRequest(url: endpoint.url(relativeTo: configuration.baseURL))

        request.httpMethod = endpoint.method.rawValue
        request.httpBody = endpoint.body
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        if endpoint.body != nil {
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        }

        if endpoint.requiresAuthentication, let token = await tokenProvider.currentToken() {
            request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        }

        let data: Data
        let response: URLResponse
        do {
            (data, response) = try await session.data(for: request)
        } catch is CancellationError {
            throw APIError.cancelled
        } catch let urlError as URLError where urlError.code == .cancelled {
            throw APIError.cancelled
        } catch {
            throw APIError.transport(underlying: error)
        }

        guard let http = response as? HTTPURLResponse else {
            throw APIError.transport(underlying: URLError(.badServerResponse))
        }

        AtlasLog.networking.debug(
            "\(endpoint.method.rawValue, privacy: .public) \(endpoint.path, privacy: .public) -> \(http.statusCode, privacy: .public)"
        )

        switch http.statusCode {
        case 200 ... 299:
            return data

        case 401, 403:
            throw APIError.unauthorized(problem: Self.decodeProblem(from: data))

        case 429:
            let retryAfter = (http.value(forHTTPHeaderField: "Retry-After")).flatMap(TimeInterval.init)
            throw APIError.rateLimited(retryAfter: retryAfter)

        default:
            throw APIError.server(status: http.statusCode, problem: Self.decodeProblem(from: data))
        }
    }

    private static func decodeProblem(from data: Data) -> ProblemDetails? {
        guard !data.isEmpty else { return nil }
        return try? AtlasJSON.decoder.decode(ProblemDetails.self, from: data)
    }
}
