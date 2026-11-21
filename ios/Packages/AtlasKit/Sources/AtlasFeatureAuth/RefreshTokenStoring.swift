import AtlasNetworking
import Foundation

/// Somewhere to keep the refresh token between launches.
///
/// Note the name: `storedRefreshToken()` only reads what is saved. It is
/// deliberately not called `refreshToken()`, because `TokenProviding` already
/// uses that name for performing an exchange, and one type implements both.
///
/// The app shell supplies a Keychain-backed implementation. This protocol
/// exists so the store can be tested without touching the real Keychain,
/// which is awkward in a unit test and shared across runs.
public protocol RefreshTokenStoring: Sendable {
    func storedRefreshToken() async -> String?

    /// Records both tokens from a sign-in.
    ///
    /// The access token is handed over too, not just the refresh token:
    /// without it the very next request goes out unauthenticated, gets a 401,
    /// and the user is told their session expired seconds after signing in.
    func store(accessToken: String, refreshToken: String) async

    func clear() async
}

/// Keeps the token in memory only. For tests and previews.
public actor InMemoryRefreshTokenStore: RefreshTokenStoring {
    private var token: String?

    public init(token: String? = nil) { self.token = token }

    public private(set) var accessToken: String?

    public func storedRefreshToken() async -> String? { token }

    public func store(accessToken: String, refreshToken: String) async {
        self.accessToken = accessToken
        token = refreshToken
    }

    public func clear() async {
        accessToken = nil
        token = nil
    }
}
