import AtlasNetworking
import Foundation

/// Reads and writes users.
///
/// The seam between the feature and the network. A view model talks to this,
/// so feature tests need a seven-line stub rather than a URL protocol mock.
public protocol UsersRepository: Sendable {
    /// Fetches one page of users.
    func users(limit: Int, cursor: String?) async throws(APIError) -> UserPage

    /// Registers a user.
    func createUser(_ request: CreateUserRequest) async throws(APIError) -> User
}

/// Talks to the Atlas API.
public struct LiveUsersRepository: UsersRepository {
    private let client: any APIClient

    public init(client: any APIClient) {
        self.client = client
    }

    public func users(limit: Int, cursor: String?) async throws(APIError) -> UserPage {
        var query = [URLQueryItem(name: "limit", value: String(limit))]
        if let cursor {
            query.append(URLQueryItem(name: "cursor", value: cursor))
        }

        return try await client.send(
            Endpoint(path: "users", query: query),
            as: UserPage.self
        )
    }

    public func createUser(_ request: CreateUserRequest) async throws(APIError) -> User {
        let endpoint: Endpoint
        do {
            endpoint = try Endpoint.json(method: .post, path: "users", body: request)
        } catch {
            throw APIError.decoding(underlying: error)
        }

        return try await client.send(endpoint, as: User.self)
    }
}
