import Foundation

/// Credentials returned by the API after registration or sign-in.
public struct AuthTokens: Decodable, Sendable, Equatable {
    public let accessToken: String
    public let refreshToken: String
    public let expiresIn: Int
    public let tokenType: String

    public init(accessToken: String, refreshToken: String, expiresIn: Int, tokenType: String = "Bearer") {
        self.accessToken = accessToken
        self.refreshToken = refreshToken
        self.expiresIn = expiresIn
        self.tokenType = tokenType
    }
}

/// The signed-in user, as `GET /auth/me` reports them.
public struct CurrentUser: Decodable, Sendable, Equatable, Identifiable {
    public let id: UUID
    public let email: String
    public let displayName: String
    public let role: String

    public init(id: UUID, email: String, displayName: String, role: String) {
        self.id = id
        self.email = email
        self.displayName = displayName
        self.role = role
    }
}

/// A deliberately uninformative acknowledgement from the API.
public struct Acknowledgement: Decodable, Sendable, Equatable {
    public let message: String
    public init(message: String) { self.message = message }
}

struct RegisterBody: Encodable, Sendable {
    let email: String
    let password: String
    let displayName: String
}

struct LoginBody: Encodable, Sendable {
    let email: String
    let password: String
}

struct RefreshBody: Encodable, Sendable {
    let refreshToken: String
}
