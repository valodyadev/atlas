import AtlasNetworking
import Foundation
import Security

/// Stores the access token in the Keychain.
///
/// The Keychain, not `UserDefaults`: tokens in `UserDefaults` end up in
/// unencrypted backups and in any file-system dump. `ThisDeviceOnly`
/// accessibility keeps the token from migrating to a restored device.
///
/// Refresh is intentionally unimplemented: wire it to your identity provider's
/// token endpoint. Until then the app runs against a backend with
/// `Auth:Enabled=false`, which is the starter's default.
actor KeychainTokenStore: TokenProviding {
    private let service: String
    private let account = "access-token"

    init(service: String = Bundle.main.bundleIdentifier ?? "com.atlas.app") {
        self.service = service
    }

    func currentToken() async -> String? {
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

    func refreshToken() async -> String? {
        // TODO: exchange the refresh token with your identity provider.
        // Returning nil makes the client surface .unauthorized, which the UI
        // turns into a sign-in prompt - the correct behaviour until this is
        // implemented.
        nil
    }

    /// Stores a token, replacing any existing one.
    func store(_ token: String) throws {
        try remove()

        var attributes = baseQuery()
        attributes[kSecValueData as String] = Data(token.utf8)
        attributes[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly

        let status = SecItemAdd(attributes as CFDictionary, nil)
        guard status == errSecSuccess else {
            throw KeychainError.unexpectedStatus(status)
        }
    }

    /// Deletes the stored token. Called on sign-out.
    func remove() throws {
        let status = SecItemDelete(baseQuery() as CFDictionary)
        guard status == errSecSuccess || status == errSecItemNotFound else {
            throw KeychainError.unexpectedStatus(status)
        }
    }

    private func baseQuery() -> [String: Any] {
        [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
        ]
    }

    enum KeychainError: Error {
        case unexpectedStatus(OSStatus)
    }
}
