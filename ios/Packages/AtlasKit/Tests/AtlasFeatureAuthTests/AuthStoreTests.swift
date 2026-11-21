import AtlasNetworking
import Foundation
import Testing
@testable import AtlasFeatureAuth

/// Hand-written stub. No network, no Keychain.
private final class StubAuthRepository: AuthRepository, @unchecked Sendable {
    var registerResult: Result<Acknowledgement, APIError> = .success(Acknowledgement(message: "ok"))
    var loginResult: Result<AuthTokens, APIError> = .failure(.unauthorized(problem: nil))
    var currentUserResult: Result<CurrentUser, APIError> = .failure(.unauthorized(problem: nil))
    private(set) var loggedOutTokens: [String] = []

    func register(email: String, password: String, displayName: String) async throws(APIError) -> Acknowledgement {
        switch registerResult {
        case let .success(value): value
        case let .failure(error): throw error
        }
    }

    func logIn(email: String, password: String) async throws(APIError) -> AuthTokens {
        switch loginResult {
        case let .success(value): value
        case let .failure(error): throw error
        }
    }

    func currentUser() async throws(APIError) -> CurrentUser {
        switch currentUserResult {
        case let .success(value): value
        case let .failure(error): throw error
        }
    }

    func logOut(refreshToken: String) async throws(APIError) {
        loggedOutTokens.append(refreshToken)
    }
}

private let someUser = CurrentUser(
    id: UUID(), email: "ada@example.com", displayName: "Ada", role: "member"
)

private let someTokens = AuthTokens(
    accessToken: "access", refreshToken: "refresh", expiresIn: 900
)

@Suite("Auth store")
@MainActor
struct AuthStoreTests {
    private func makeStore(
        repository: StubAuthRepository,
        storedToken: String? = nil
    ) -> (AuthStore, InMemoryRefreshTokenStore) {
        let credentials = InMemoryRefreshTokenStore(token: storedToken)
        return (AuthStore(repository: repository, credentialStore: credentials), credentials)
    }

    @Test("Starts in restoring, so the UI never flashes the sign-in screen")
    func startsRestoring() {
        let (store, _) = makeStore(repository: StubAuthRepository())

        #expect(store.state == .restoring)
    }

    @Test("With no stored token, restoring lands on signed out")
    func restoreWithoutToken() async {
        let (store, _) = makeStore(repository: StubAuthRepository())

        await store.restore()

        #expect(store.state == .signedOut)
    }

    @Test("A stored token is only trusted if the API still accepts it")
    func restoreValidatesWithTheServer() async {
        let repository = StubAuthRepository()
        repository.currentUserResult = .success(someUser)
        let (store, _) = makeStore(repository: repository, storedToken: "stored")

        await store.restore()

        #expect(store.state == .signedIn(someUser))
    }

    @Test("A stored token the server rejects is discarded, not kept")
    func restoreClearsRejectedToken() async {
        // Otherwise a revoked session would be retried on every launch forever.
        let repository = StubAuthRepository()
        repository.currentUserResult = .failure(.unauthorized(problem: nil))
        let (store, credentials) = makeStore(repository: repository, storedToken: "revoked")

        await store.restore()

        #expect(store.state == .signedOut)
        #expect(await credentials.storedRefreshToken() == nil)
    }

    @Test("Signing in stores the refresh token and reports the user")
    func signInSucceeds() async {
        let repository = StubAuthRepository()
        repository.loginResult = .success(someTokens)
        repository.currentUserResult = .success(someUser)
        let (store, credentials) = makeStore(repository: repository)

        await store.signIn(email: "ada@example.com", password: "correct horse battery staple")

        #expect(store.state == .signedIn(someUser))
        #expect(store.formState == .idle)
        #expect(await credentials.storedRefreshToken() == "refresh")
    }

    @Test("Signing in keeps the access token, not only the refresh token")
    func signInStoresTheAccessToken() async {
        // Regression guard. Storing only the refresh token meant the next
        // request went out unauthenticated, got a 401, and the person was told
        // "your session has expired" seconds after signing in successfully.
        let repository = StubAuthRepository()
        repository.loginResult = .success(someTokens)
        repository.currentUserResult = .success(someUser)
        let (store, credentials) = makeStore(repository: repository)

        await store.signIn(email: "ada@example.com", password: "correct horse battery staple")

        #expect(await credentials.accessToken == "access")
        #expect(await credentials.storedRefreshToken() == "refresh")
    }

    @Test("Signing out forgets the access token too")
    func signOutClearsTheAccessToken() async {
        let repository = StubAuthRepository()
        repository.loginResult = .success(someTokens)
        repository.currentUserResult = .success(someUser)
        let (store, credentials) = makeStore(repository: repository)
        await store.signIn(email: "ada@example.com", password: "correct horse battery staple")

        await store.signOut()

        #expect(await credentials.accessToken == nil)
        #expect(await credentials.storedRefreshToken() == nil)
    }

    @Test("A rejected sign-in shows a message and stores nothing")
    func signInFails() async {
        let repository = StubAuthRepository()
        repository.loginResult = .failure(.unauthorized(problem: nil))
        let (store, credentials) = makeStore(repository: repository)

        await store.signIn(email: "ada@example.com", password: "wrong")

        guard case let .failed(message) = store.formState else {
            Issue.record("Expected a failure, got \(store.formState)")
            return
        }
        #expect(!message.isEmpty)
        #expect(store.state == .restoring) // never claims to be signed in
        #expect(await credentials.storedRefreshToken() == nil)
    }

    @Test("Creating an account signs straight in")
    func createAccountSignsIn() async {
        let repository = StubAuthRepository()
        repository.loginResult = .success(someTokens)
        repository.currentUserResult = .success(someUser)
        let (store, credentials) = makeStore(repository: repository)

        await store.createAccount(
            email: "ada@example.com", password: "correct horse battery staple", displayName: "Ada"
        )

        #expect(store.state == .signedIn(someUser))
        #expect(await credentials.storedRefreshToken() == "refresh")
    }

    @Test("Signing out revokes server-side and clears the token")
    func signOutRevokes() async {
        let repository = StubAuthRepository()
        repository.loginResult = .success(someTokens)
        repository.currentUserResult = .success(someUser)
        let (store, credentials) = makeStore(repository: repository)
        await store.signIn(email: "ada@example.com", password: "correct horse battery staple")

        await store.signOut()

        #expect(store.state == .signedOut)
        #expect(await credentials.storedRefreshToken() == nil)
        #expect(repository.loggedOutTokens == ["refresh"])
    }

    @Test("Sign-out clears locally even when the server call fails")
    func signOutIsLocalFirst() async {
        // Leaving someone apparently signed in because the network blipped is
        // worse than a refresh token outliving the UI state.
        final class FailingLogout: AuthRepository, @unchecked Sendable {
            func register(
                email: String,
                password: String,
                displayName: String
            ) async throws(APIError) -> Acknowledgement {
                Acknowledgement(message: "ok")
            }

            func logIn(email: String, password: String) async throws(APIError) -> AuthTokens { someTokens }
            func currentUser() async throws(APIError) -> CurrentUser { someUser }
            func logOut(refreshToken: String) async throws(APIError) {
                throw APIError.transport(underlying: URLError(.notConnectedToInternet))
            }
        }

        let credentials = InMemoryRefreshTokenStore(token: "refresh")
        let store = AuthStore(repository: FailingLogout(), credentialStore: credentials)

        await store.signOut()

        #expect(store.state == .signedOut)
        #expect(await credentials.storedRefreshToken() == nil)
    }

    @Test("Editing a field clears a stale error")
    func clearError() async {
        let repository = StubAuthRepository()
        repository.loginResult = .failure(.unauthorized(problem: nil))
        let (store, _) = makeStore(repository: repository)
        await store.signIn(email: "a@b.com", password: "nope")

        store.clearError()

        #expect(store.formState == .idle)
    }
}
