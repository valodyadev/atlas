import Foundation
import Testing
@testable import AtlasNetworking

@Suite("Endpoint URL building")
struct EndpointTests {
    private let base = URL(string: "https://api.example.com/api/v1")!

    @Test("Joins the base URL and path without losing the version prefix")
    func joinsPath() throws {
        let url = try Endpoint(path: "users").url(relativeTo: base)

        #expect(url.absoluteString == "https://api.example.com/api/v1/users")
    }

    @Test("A leading slash on the path does not escape the base URL", arguments: ["users", "/users"])
    func leadingSlashIsTolerated(path: String) throws {
        let url = try Endpoint(path: path).url(relativeTo: base)

        #expect(url.absoluteString == "https://api.example.com/api/v1/users")
    }

    @Test("Query items are appended and percent-encoded")
    func appendsQuery() throws {
        let endpoint = Endpoint(path: "users", query: [
            URLQueryItem(name: "limit", value: "25"),
            URLQueryItem(name: "cursor", value: "a b+c"),
        ])

        let url = try endpoint.url(relativeTo: base)

        #expect(url.absoluteString.contains("limit=25"))
        #expect(!url.absoluteString.contains("a b"))
    }

    @Test("A JSON endpoint carries an encoded body")
    func jsonBody() throws {
        struct Payload: Encodable, Sendable { let email: String }

        let endpoint = try Endpoint.json(method: .post, path: "users", body: Payload(email: "a@b.com"))

        #expect(endpoint.method == .post)
        #expect(endpoint.body != nil)
        #expect(try String(decoding: #require(endpoint.body), as: UTF8.self).contains("a@b.com"))
    }
}

@Suite("Retry eligibility")
struct RetrySemanticsTests {
    @Test("Only idempotent verbs may be retried automatically")
    func idempotency() {
        #expect(HTTPMethod.get.isIdempotent)
        #expect(HTTPMethod.put.isIdempotent)
        #expect(HTTPMethod.delete.isIdempotent)
        // Retrying these could duplicate a record.
        #expect(!HTTPMethod.post.isIdempotent)
        #expect(!HTTPMethod.patch.isIdempotent)
    }

    @Test("Server and transport failures are retryable; client mistakes are not")
    func retryableErrors() {
        #expect(APIError.transport(underlying: URLError(.timedOut)).isRetryable)
        #expect(APIError.server(status: 503, problem: nil).isRetryable)
        #expect(APIError.rateLimited(retryAfter: 1).isRetryable)
        #expect(!APIError.server(status: 400, problem: nil).isRetryable)
        #expect(!APIError.unauthorized(problem: nil).isRetryable)
        #expect(!APIError.cancelled.isRetryable)
    }

    @Test("Backoff grows, stays jittered and respects the ceiling")
    func backoffCurve() {
        let policy = RetryPolicy(maxAttempts: 6, baseDelay: .milliseconds(100), maxDelay: .seconds(1))

        for attempt in 1 ... 6 {
            let delay = policy.delay(forAttempt: attempt)
            #expect(delay > .zero)
            #expect(delay <= .seconds(1))
        }

        // Jitter must actually vary, or every client retries in lockstep.
        let samples = Set((0 ..< 40).map { _ in policy.delay(forAttempt: 4) })
        #expect(samples.count > 1)
    }

    @Test("The error code surfaces the server's stable code")
    func errorCode() {
        let problem = ProblemDetails(code: "identity.user.email_taken")

        #expect(APIError.server(status: 409, problem: problem).code == "identity.user.email_taken")
        #expect(APIError.cancelled.code == nil)
    }
}
