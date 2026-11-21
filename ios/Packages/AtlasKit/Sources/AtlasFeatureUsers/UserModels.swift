import Foundation

/// A user, as returned by `GET /api/v1/users`.
///
/// Mirrors `Atlas.Modules.Identity.Contracts.UserResponse`. See
/// docs/adr/0008-api-contract-sharing.md for how this stays in step with the
/// backend and how to move to generated models.
public struct User: Identifiable, Decodable, Sendable, Equatable {
    public let id: UUID
    public let email: String
    public let displayName: String
    public let createdAt: Date

    public init(id: UUID, email: String, displayName: String, createdAt: Date) {
        self.id = id
        self.email = email
        self.displayName = displayName
        self.createdAt = createdAt
    }
}

/// A page of users plus the cursor for the next one.
public struct UserPage: Decodable, Sendable, Equatable {
    public let items: [User]
    public let nextCursor: String?

    public init(items: [User], nextCursor: String?) {
        self.items = items
        self.nextCursor = nextCursor
    }
}

/// Body for `POST /api/v1/users`.
public struct CreateUserRequest: Encodable, Sendable, Equatable {
    public let email: String
    public let displayName: String

    public init(email: String, displayName: String) {
        self.email = email
        self.displayName = displayName
    }
}
