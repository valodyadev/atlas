import AtlasCore
import Foundation
import Testing
@testable import AtlasNetworking

@Suite("URLSessionAPIClient", .serialized)
struct URLSessionAPIClientTests {
    private struct Payload: Decodable, Sendable, Equatable {
        let id: String
    }

    private func makeClient(
        retryPolicy: RetryPolicy = .none,
        tokenProvider: any TokenProviding = AnonymousTokenProvider()
    ) -> URLSessionAPIClient {
        StubURLProtocol.reset()
        return URLSessionAPIClient(
            configuration: APIConfiguration(
                baseURL: URL(string: "https://api.example.com/api/v1")!,
                environment: .development
            ),
            tokenProvider: tokenProvider,
            retryPolicy: retryPolicy,
            session: StubURLProtocol.makeSession()
        )
    }

    @Test("Decodes a 200 response")
    func decodesSuccess() async throws {
        let client = makeClient()
        StubURLProtocol.enqueue(.init(body: Data(#"{"id":"abc"}"#.utf8)))

        let payload = try await client.send(Endpoint(path: "things"), as: Payload.self)

        #expect(payload == Payload(id: "abc"))
    }

    @Test("A 409 surfaces the stable error code")
    func surfacesErrorCode() async throws {
        let client = makeClient()
        StubURLProtocol.enqueue(.init(
            statusCode: 409,
            body: Data(#"{"status":409,"code":"identity.user.email_taken"}"#.utf8)
        ))

        do {
            _ = try await client.send(Endpoint(path: "users"), as: Payload.self)
            Issue.record("Expected the call to throw")
        } catch {
            #expect(error.code == "identity.user.email_taken")
            guard case let .server(status, _) = error else {
                Issue.record("Expected a server error, got \(error)")
                return
            }
            #expect(status == 409)
        }
    }

    @Test("A 429 is reported as rate limiting with Retry-After")
    func mapsRateLimit() async throws {
        let client = makeClient()
        StubURLProtocol.enqueue(.init(statusCode: 429, headers: ["Retry-After": "42"]))

        do {
            _ = try await client.send(Endpoint(path: "users"), as: Payload.self)
            Issue.record("Expected the call to throw")
        } catch {
            guard case let .rateLimited(retryAfter) = error else {
                Issue.record("Expected rate limiting, got \(error)")
                return
            }
            #expect(retryAfter == 42)
        }
    }

    @Test("A malformed body is a decoding error, not a success")
    func mapsDecodingFailure() async throws {
        let client = makeClient()
        StubURLProtocol.enqueue(.init(body: Data(#"{"unexpected":true}"#.utf8)))

        do {
            _ = try await client.send(Endpoint(path: "things"), as: Payload.self)
            Issue.record("Expected the call to throw")
        } catch {
            guard case .decoding = error else {
                Issue.record("Expected a decoding error, got \(error)")
                return
            }
        }
    }

    @Test("A GET retries a 500 and then succeeds")
    func retriesIdempotentRequests() async throws {
        let client = makeClient(
            retryPolicy: RetryPolicy(maxAttempts: 3, baseDelay: .milliseconds(1), maxDelay: .milliseconds(5))
        )

        StubURLProtocol.enqueue(
            .init(statusCode: 500),
            .init(statusCode: 500),
            .init(body: Data(#"{"id":"recovered"}"#.utf8))
        )

        let payload = try await client.send(Endpoint(path: "things"), as: Payload.self)

        #expect(payload.id == "recovered")
        #expect(StubURLProtocol.requests.count == 3)
    }

    @Test("A POST is never retried, because it could duplicate a record")
    func doesNotRetryPost() async throws {
        let client = makeClient(
            retryPolicy: RetryPolicy(maxAttempts: 3, baseDelay: .milliseconds(1))
        )

        StubURLProtocol.enqueue(
            .init(statusCode: 500),
            .init(body: Data(#"{"id":"should-not-be-reached"}"#.utf8))
        )

        do {
            _ = try await client.send(
                Endpoint.json(method: .post, path: "users", body: ["email": "a@b.com"]),
                as: Payload.self
            )
            Issue.record("Expected the call to throw")
        } catch {
            #expect(StubURLProtocol.requests.count == 1)
        }
    }

    @Test("A bearer token is attached when one is available")
    func attachesBearerToken() async throws {
        struct StaticToken: TokenProviding {
            func currentToken() async -> String? { "test-token" }
            func refreshToken() async -> String? { nil }
        }

        let client = makeClient(tokenProvider: StaticToken())
        StubURLProtocol.enqueue(.init(body: Data(#"{"id":"abc"}"#.utf8)))

        _ = try await client.send(Endpoint(path: "things"), as: Payload.self)

        let header = StubURLProtocol.requests.first?.value(forHTTPHeaderField: "Authorization")
        #expect(header == "Bearer test-token")
    }

    @Test("No Authorization header is sent for an anonymous endpoint")
    func omitsTokenWhenNotRequired() async throws {
        let client = makeClient()
        StubURLProtocol.enqueue(.init(body: Data(#"{"id":"abc"}"#.utf8)))

        _ = try await client.send(
            Endpoint(path: "meta", requiresAuthentication: false), as: Payload.self
        )

        #expect(StubURLProtocol.requests.first?.value(forHTTPHeaderField: "Authorization") == nil)
    }

    @Test("A 401 triggers exactly one refresh attempt")
    func refreshesOnceOn401() async throws {
        actor RefreshCounter {
            private(set) var count = 0
            func increment() { count += 1 }
        }

        struct CountingToken: TokenProviding {
            let counter: RefreshCounter
            func currentToken() async -> String? { "stale" }
            func refreshToken() async -> String? {
                await counter.increment()
                return nil
            }
        }

        let counter = RefreshCounter()
        let client = makeClient(tokenProvider: CountingToken(counter: counter))
        StubURLProtocol.enqueue(.init(statusCode: 401), .init(statusCode: 401))

        do {
            _ = try await client.send(Endpoint(path: "things"), as: Payload.self)
            Issue.record("Expected the call to throw")
        } catch {
            guard case .unauthorized = error else {
                Issue.record("Expected unauthorized, got \(error)")
                return
            }
        }

        #expect(await counter.count == 1)
    }
}
