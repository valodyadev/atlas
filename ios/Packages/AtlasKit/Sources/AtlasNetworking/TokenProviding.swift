import Foundation

/// Supplies and refreshes the bearer token.
///
/// Deliberately a protocol with no storage opinion: the app shell decides
/// where the token lives (Keychain in production, in-memory in tests). Tokens
/// must never be written to `UserDefaults` or to a log.
public protocol TokenProviding: Sendable {
    /// The current access token, or `nil` when signed out.
    func currentToken() async -> String?

    /// Attempts a refresh after a 401.
    /// - Returns: A new token, or `nil` if the user must sign in again.
    func refreshToken() async -> String?
}

/// Always signed out. The default until an identity provider is wired up.
public struct AnonymousTokenProvider: TokenProviding {
    public init() {}
    public func currentToken() async -> String? { nil }
    public func refreshToken() async -> String? { nil }
}
