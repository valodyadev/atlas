import AtlasFeatureAuth
import AtlasNetworking
import Foundation
import Security

/// Holds the session's credentials.
///
/// Two very different things live here, stored differently on purpose:
///
/// * the **refresh token** goes in the Keychain, because it must survive
///   relaunches and is the thing worth protecting;
/// * the **access token** stays in memory only, because it expires in minutes
///   and writing it to disk would add risk for no benefit.
///
/// The Keychain, not `UserDefaults`: values stored there end up in unencrypted
/// backups and in any file-system dump. `ThisDeviceOnly` stops the token
/// migrating to a restored device.
actor KeychainTokenStore: TokenProviding, RefreshTokenStoring {
    /// Exchanges a refresh token for a new pair.
    ///
    /// Injected rather than built here, because the exchange needs an API
    /// client and the API client needs this store — a cycle. The handler is
    /// supplied by the composition root using a separate, unauthenticated
    /// client, which also stops a failing refresh from recursing into itself.
    typealias RefreshHandler = @Sendable (String) async -> AuthTokens?

    private let service: String
    private let account = "refresh-token"
    private var accessToken: String?
    private let refreshHandler: RefreshHandler

    init(
        refreshHandler: @escaping RefreshHandler,
        service: String = Bundle.main.bundleIdentifier ?? "com.atlas.app"
    ) {
        self.refreshHandler = refreshHandler
        self.service = service
    }

    // MARK: - TokenProviding

    func currentToken() async -> String? { accessToken }

    func refreshToken() async -> String? {
        guard let stored = readRefreshToken() else {
            return nil
        }

        guard let tokens = await refreshHandler(stored) else {
            // The refresh token is spent, expired or revoked. Keeping it would
            // mean retrying a dead credential on every request.
            clearStoredRefreshToken()
            accessToken = nil
            return nil
        }

        accessToken = tokens.accessToken
        writeRefreshToken(tokens.refreshToken)
        return tokens.accessToken
    }

    // MARK: - RefreshTokenStoring

    func storedRefreshToken() async -> String? { readRefreshToken() }

    func store(accessToken token: String, refreshToken refresh: String) async {
        accessToken = token
        writeRefreshToken(refresh)
    }

    func clear() async {
        clearStoredRefreshToken()
        accessToken = nil
    }

    // MARK: - Keychain

    private func readRefreshToken() -> String? {
        var query = baseQuery()
        query[kSecReturnData as String] = true
        query[kSecMatchLimit as String] = kSecMatchLimitOne

        var item: CFTypeRef?
        guard SecItemCopyMatching(query as CFDictionary, &item) == errSecSuccess,
              let data = item as? Data
        else {
            return nil
        }
        return String(data: data, encoding: .utf8)
    }

    private func writeRefreshToken(_ token: String) {
        clearStoredRefreshToken()
        var attributes = baseQuery()
        attributes[kSecValueData as String] = Data(token.utf8)
        attributes[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
        SecItemAdd(attributes as CFDictionary, nil)
    }

    private func clearStoredRefreshToken() {
        SecItemDelete(baseQuery() as CFDictionary)
    }

    private func baseQuery() -> [String: Any] {
        [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
        ]
    }
}
