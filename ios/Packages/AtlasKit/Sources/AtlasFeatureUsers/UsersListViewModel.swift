import AtlasCore
import AtlasNetworking
import Foundation
import Observation

/// Drives the users list screen.
///
/// `@Observable` + `@MainActor`: the view reads state directly and SwiftUI
/// tracks exactly the properties each view body touches, so there are no
/// `@Published` declarations to keep in step and no accidental full-screen
/// invalidations. All state mutation is main-actor-isolated, which under the
/// Swift 6 language mode is checked by the compiler rather than hoped for.
@MainActor
@Observable
public final class UsersListViewModel {
    /// What the screen should currently render.
    public enum ViewState: Equatable, Sendable {
        case idle
        case loading
        case loaded(users: [User])
        case empty
        case failed(message: String)
    }

    public private(set) var state: ViewState = .idle
    public private(set) var isLoadingMore = false

    private let repository: any UsersRepository
    private let telemetry: any Telemetry
    private let pageSize: Int

    private var users: [User] = []
    private var nextCursor: String?
    private var loadTask: Task<Void, Never>?

    public init(
        repository: any UsersRepository,
        telemetry: any Telemetry = NoopTelemetry(),
        pageSize: Int = 25
    ) {
        self.repository = repository
        self.telemetry = telemetry
        self.pageSize = pageSize
    }

    /// Loads the first page. Safe to call repeatedly: an in-flight load is
    /// cancelled so a fast pull-to-refresh cannot interleave two responses and
    /// leave the list showing the older one.
    public func load() async {
        loadTask?.cancel()

        let task = Task { @MainActor [weak self] in
            guard let self else { return }
            await performInitialLoad()
        }
        loadTask = task
        await task.value
    }

    /// Loads the next page, if there is one.
    public func loadMore() async {
        guard let cursor = nextCursor, !isLoadingMore else { return }

        isLoadingMore = true
        defer { isLoadingMore = false }

        do {
            let page = try await repository.users(limit: pageSize, cursor: cursor)
            users.append(contentsOf: page.items)
            nextCursor = page.nextCursor
            state = users.isEmpty ? .empty : .loaded(users: users)
        } catch {
            // A failed "load more" must not destroy the page the user is
            // already reading; surface it without clearing the list.
            telemetry.record(error: error, context: "users.loadMore")
            AtlasLog.feature.error("load more failed: \(error.localizedDescription, privacy: .public)")
        }
    }

    /// Adds a user and puts it at the top of the list.
    public func createUser(email: String, displayName: String) async -> Bool {
        do {
            let created = try await repository.createUser(
                CreateUserRequest(email: email, displayName: displayName)
            )

            users.insert(created, at: 0)
            state = .loaded(users: users)
            telemetry.record(event: "users.created")
            return true
        } catch {
            telemetry.record(error: error, context: "users.create")
            state = .failed(message: error.localizedDescription)
            return false
        }
    }

    private func performInitialLoad() async {
        state = .loading

        do {
            let page = try await repository.users(limit: pageSize, cursor: nil)
            guard !Task.isCancelled else { return }

            users = page.items
            nextCursor = page.nextCursor
            state = users.isEmpty ? .empty : .loaded(users: users)
            telemetry.record(event: "users.list.viewed", metadata: ["count": String(users.count)])
        } catch APIError.cancelled {
            // Superseded by a newer load; leave the state alone.
        } catch {
            guard !Task.isCancelled else { return }
            telemetry.record(error: error, context: "users.load")
            state = .failed(message: error.localizedDescription)
        }
    }
}
