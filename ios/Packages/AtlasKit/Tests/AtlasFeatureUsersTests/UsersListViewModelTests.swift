import AtlasCore
import AtlasNetworking
import Foundation
import Testing
@testable import AtlasFeatureUsers

/// Hand-written repository stub. Seven lines, no network, no URL protocol -
/// which is the point of depending on `UsersRepository` rather than on
/// `URLSession`.
private struct StubUsersRepository: UsersRepository {
    var pages: [String?: UserPage] = [:]
    var listError: APIError?
    var createResult: Result<User, APIError> = .failure(.cancelled)

    func users(limit: Int, cursor: String?) async throws(APIError) -> UserPage {
        if let listError {
            throw listError
        }
        return pages[cursor] ?? UserPage(items: [], nextCursor: nil)
    }

    func createUser(_ request: CreateUserRequest) async throws(APIError) -> User {
        switch createResult {
        case let .success(user): user
        case let .failure(error): throw error
        }
    }
}

private func makeUser(_ name: String) -> User {
    User(
        id: UUID(),
        email: "\(name.lowercased())@example.com",
        displayName: name,
        createdAt: Date(timeIntervalSince1970: 1_800_000_000)
    )
}

@Suite("Users list view model")
@MainActor
struct UsersListViewModelTests {
    @Test("Starts idle and does not fetch until asked")
    func startsIdle() {
        let viewModel = UsersListViewModel(repository: StubUsersRepository())

        #expect(viewModel.state == .idle)
    }

    @Test("A successful load shows the users")
    func loadsUsers() async {
        let users = [makeUser("Ada"), makeUser("Grace")]
        var repository = StubUsersRepository()
        repository.pages = [nil: UserPage(items: users, nextCursor: nil)]

        let viewModel = UsersListViewModel(repository: repository)
        await viewModel.load()

        #expect(viewModel.state == .loaded(users: users))
    }

    @Test("An empty result is a distinct state, not an empty list")
    func distinguishesEmpty() async {
        var repository = StubUsersRepository()
        repository.pages = [nil: UserPage(items: [], nextCursor: nil)]

        let viewModel = UsersListViewModel(repository: repository)
        await viewModel.load()

        // The view renders a call-to-action for .empty and a spinner-free list
        // for .loaded([]) - conflating them is how you ship a blank screen.
        #expect(viewModel.state == .empty)
    }

    @Test("A failed load surfaces a readable message")
    func surfacesFailure() async {
        var repository = StubUsersRepository()
        repository.listError = .transport(underlying: URLError(.notConnectedToInternet))

        let viewModel = UsersListViewModel(repository: repository)
        await viewModel.load()

        guard case let .failed(message) = viewModel.state else {
            Issue.record("Expected a failure state, got \(viewModel.state)")
            return
        }
        #expect(!message.isEmpty)
    }

    @Test("Load more appends the next page")
    func appendsNextPage() async {
        let first = [makeUser("Ada")]
        let second = [makeUser("Grace")]
        var repository = StubUsersRepository()
        repository.pages = [
            nil: UserPage(items: first, nextCursor: "cursor-1"),
            "cursor-1": UserPage(items: second, nextCursor: nil),
        ]

        let viewModel = UsersListViewModel(repository: repository)
        await viewModel.load()
        await viewModel.loadMore()

        #expect(viewModel.state == .loaded(users: first + second))
        #expect(!viewModel.isLoadingMore)
    }

    @Test("Load more stops at the end of the collection")
    func stopsAtTheEnd() async {
        let users = [makeUser("Ada")]
        var repository = StubUsersRepository()
        repository.pages = [nil: UserPage(items: users, nextCursor: nil)]

        let viewModel = UsersListViewModel(repository: repository)
        await viewModel.load()
        await viewModel.loadMore()
        await viewModel.loadMore()

        #expect(viewModel.state == .loaded(users: users))
    }

    @Test("A failed load-more keeps the page the user is already reading")
    func failedLoadMoreKeepsExistingPage() async {
        // Regression guard: clearing the list on a pagination failure makes the
        // app look broken when the user has done nothing wrong.
        final class FlakyRepository: UsersRepository, @unchecked Sendable {
            let first: [User]
            private var callCount = 0

            init(first: [User]) { self.first = first }

            func users(limit: Int, cursor: String?) async throws(APIError) -> UserPage {
                callCount += 1
                if callCount == 1 {
                    return UserPage(items: first, nextCursor: "cursor-1")
                }
                throw APIError.server(status: 503, problem: nil)
            }

            func createUser(_ request: CreateUserRequest) async throws(APIError) -> User {
                throw APIError.cancelled
            }
        }

        let users = [makeUser("Ada")]
        let viewModel = UsersListViewModel(repository: FlakyRepository(first: users))

        await viewModel.load()
        await viewModel.loadMore()

        #expect(viewModel.state == .loaded(users: users))
        #expect(!viewModel.isLoadingMore)
    }

    @Test("Creating a user puts it at the top of the list")
    func createPrepends() async {
        let existing = [makeUser("Ada")]
        let created = makeUser("Grace")
        var repository = StubUsersRepository()
        repository.pages = [nil: UserPage(items: existing, nextCursor: nil)]
        repository.createResult = .success(created)

        let viewModel = UsersListViewModel(repository: repository)
        await viewModel.load()

        let didCreate = await viewModel.createUser(email: created.email, displayName: created.displayName)

        #expect(didCreate)
        #expect(viewModel.state == .loaded(users: [created] + existing))
    }

    @Test("A rejected create reports failure and does not insert anything")
    func createFailureDoesNotInsert() async {
        let existing = [makeUser("Ada")]
        var repository = StubUsersRepository()
        repository.pages = [nil: UserPage(items: existing, nextCursor: nil)]
        repository.createResult = .failure(
            .server(status: 409, problem: ProblemDetails(code: "identity.user.email_taken"))
        )

        let viewModel = UsersListViewModel(repository: repository)
        await viewModel.load()

        let didCreate = await viewModel.createUser(email: "taken@example.com", displayName: "Taken")

        #expect(!didCreate)
        guard case .failed = viewModel.state else {
            Issue.record("Expected a failure state, got \(viewModel.state)")
            return
        }
    }

    @Test("Telemetry records a list view with the row count")
    func recordsTelemetry() async {
        final class Recorder: Telemetry, @unchecked Sendable {
            private let lock = NSLock()
            private var captured: [String: [String: String]] = [:]

            var events: [String: [String: String]] { lock.withLock { captured } }

            func record(event name: String, metadata: [String: String]) {
                lock.withLock { captured[name] = metadata }
            }

            func record(error: Error, context: String) {}
        }

        var repository = StubUsersRepository()
        repository.pages = [nil: UserPage(items: [makeUser("Ada"), makeUser("Grace")], nextCursor: nil)]

        let recorder = Recorder()
        let viewModel = UsersListViewModel(repository: repository, telemetry: recorder)
        await viewModel.load()

        #expect(recorder.events["users.list.viewed"]?["count"] == "2")
    }
}
