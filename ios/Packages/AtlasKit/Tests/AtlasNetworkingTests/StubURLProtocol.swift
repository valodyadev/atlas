import Foundation

/// Intercepts requests so the client can be tested without a server.
///
/// A `URLProtocol` stub exercises the real `URLSession` code path - headers,
/// status handling, body decoding, retries - which a hand-written `APIClient`
/// mock would skip entirely.
final class StubURLProtocol: URLProtocol {
    struct Stub: Sendable {
        let statusCode: Int
        let body: Data
        let headers: [String: String]

        init(statusCode: Int = 200, body: Data = Data(), headers: [String: String] = [:]) {
            self.statusCode = statusCode
            self.body = body
            self.headers = headers
        }
    }

    /// All mutable state lives in one lock-guarded box.
    ///
    /// Under the Swift 6 language mode a `static var` is rejected outright,
    /// even a lock-guarded one - the compiler cannot see the lock. Hiding the
    /// state behind a single `static let` of an explicitly `@unchecked
    /// Sendable` type puts the safety argument in one auditable place instead
    /// of sprinkling `nonisolated(unsafe)` over each property.
    private final class State: @unchecked Sendable {
        private let lock = NSLock()
        private var queue: [Stub] = []
        private var observed: [URLRequest] = []

        func enqueue(_ stubs: [Stub]) {
            lock.withLock { queue.append(contentsOf: stubs) }
        }

        func reset() {
            lock.withLock {
                queue.removeAll()
                observed.removeAll()
            }
        }

        var requests: [URLRequest] {
            lock.withLock { observed }
        }

        /// Records the request and pops the next stubbed response.
        func next(for request: URLRequest) -> Stub? {
            lock.withLock {
                observed.append(request)
                return queue.isEmpty ? nil : queue.removeFirst()
            }
        }
    }

    private static let state = State()

    static func enqueue(_ stubs: Stub...) { state.enqueue(stubs) }
    static func reset() { state.reset() }
    static var requests: [URLRequest] { state.requests }

    static func makeSession() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [StubURLProtocol.self]
        return URLSession(configuration: configuration)
    }

    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }

    override func startLoading() {
        guard let url = request.url, let stub = Self.state.next(for: request) else {
            client?.urlProtocol(self, didFailWithError: URLError(.resourceUnavailable))
            return
        }

        var headers = stub.headers
        headers["Content-Type"] = headers["Content-Type"] ?? "application/json"

        guard let response = HTTPURLResponse(
            url: url,
            statusCode: stub.statusCode,
            httpVersion: "HTTP/1.1",
            headerFields: headers
        ) else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }

        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: stub.body)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}
}
