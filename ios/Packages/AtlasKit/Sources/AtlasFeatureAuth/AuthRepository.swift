import AtlasNetworking
import Foundation

/// The authentication calls the app makes.
///
/// A protocol so the view model can be tested with a small stub instead of a
/// network stack, matching how `UsersRepository` is used.
public protocol AuthRepository: Sendable {
    func register(email: String, password: String, displayName: String) async throws(APIError) -> Acknowledgement
    func logIn(email: String, password: String) async throws(APIError) -> AuthTokens
    func currentUser() async throws(APIError) -> CurrentUser
    func logOut(refreshToken: String) async throws(APIError)
}

/// Talks to the Atlas API.
public struct LiveAuthRepository: AuthRepository {
    private let client: any APIClient

    public init(client: any APIClient) {
        self.client = client
    }

    public func register(
        email: String,
        password: String,
        displayName: String
    ) async throws(APIError) -> Acknowledgement {
        try await post(
            "auth/register",
            body: RegisterBody(email: email, password: password, displayName: displayName),
            as: Acknowledgement.self
        )
    }

    public func logIn(email: String, password: String) async throws(APIError) -> AuthTokens {
        try await post("auth/login", body: LoginBody(email: email, password: password), as: AuthTokens.self)
    }

    public func currentUser() async throws(APIError) -> CurrentUser {
        try await client.send(Endpoint(path: "auth/me"), as: CurrentUser.self)
    }

    public func logOut(refreshToken: String) async throws(APIError) {
        _ = try await post("auth/logout", body: RefreshBody(refreshToken: refreshToken), as: Acknowledgement.self)
    }

    private func post<Response: Decodable & Sendable>(
        _ path: String, body: some Encodable & Sendable, as type: Response.Type
    ) async throws(APIError) -> Response {
        let endpoint: Endpoint
        do {
            // Registration and sign-in happen before there is a token.
            endpoint = try Endpoint.json(method: .post, path: path, body: body, requiresAuthentication: false)
        } catch {
            throw APIError.decoding(underlying: error)
        }
        return try await client.send(endpoint, as: type)
    }
}
