import AtlasCore
import AtlasNetworking
import Foundation
import Observation

/// Where the app is in the sign-in lifecycle.
public enum AuthState: Equatable, Sendable {
    /// Still deciding, because a stored token is being checked.
    case restoring
    /// No valid credentials. Show the sign-in screen.
    case signedOut
    /// Signed in, with the user the API reported.
    case signedIn(CurrentUser)
}

/// What the form is doing right now.
public enum AuthFormState: Equatable, Sendable {
    case idle
    case working
    case failed(message: String)
}

/// Which half of the form the person is using.
public enum AuthMode: String, CaseIterable, Sendable {
    case signIn
    case createAccount

    public var title: String {
        switch self {
        case .signIn: "Sign in"
        case .createAccount: "Create account"
        }
    }
}

/// Drives the whole authentication surface.
///
/// One object owns the session so the root view can switch on a single value.
/// The access token lives only in memory; the refresh token is the sole thing
/// written to the Keychain.
@MainActor
@Observable
public final class AuthStore {
    public private(set) var state: AuthState = .restoring
    public private(set) var formState: AuthFormState = .idle

    private let repository: any AuthRepository
    private let credentialStore: any RefreshTokenStoring
    private let telemetry: any Telemetry

    public init(
        repository: any AuthRepository,
        credentialStore: any RefreshTokenStoring,
        telemetry: any Telemetry = NoopTelemetry()
    ) {
        self.repository = repository
        self.credentialStore = credentialStore
        self.telemetry = telemetry
    }

    /// Decides at launch whether there is still a usable session.
    ///
    /// A stored refresh token is not proof of a session: it may have expired
    /// or been revoked. The only way to find out is to use it, so this asks
    /// the API rather than trusting local state.
    public func restore() async {
        guard await credentialStore.storedRefreshToken() != nil else {
            state = .signedOut
            return
        }

        do {
            state = try await .signedIn(repository.currentUser())
        } catch {
            // Includes the case where the client refreshed and still failed.
            await credentialStore.clear()
            state = .signedOut
        }
    }

    /// Creates an account, then signs straight in with the same credentials.
    public func createAccount(email: String, password: String, displayName: String) async {
        formState = .working
        do {
            _ = try await repository.register(email: email, password: password, displayName: displayName)
            telemetry.record(event: "auth.registered")
            await finishSignIn(email: email, password: password)
        } catch {
            fail(error, context: "auth.register")
        }
    }

    /// Signs in with an existing account.
    public func signIn(email: String, password: String) async {
        formState = .working
        await finishSignIn(email: email, password: password)
    }

    /// Signs out, revoking the refresh token server-side as well as locally.
    public func signOut() async {
        if let token = await credentialStore.storedRefreshToken() {
            // Best effort: if the network is down we still clear locally,
            // because leaving the person apparently signed in would be worse.
            try? await repository.logOut(refreshToken: token)
        }
        await credentialStore.clear()
        state = .signedOut
        formState = .idle
        telemetry.record(event: "auth.signed_out")
    }

    /// Clears a visible error, e.g. when the person edits a field.
    public func clearError() {
        if case .failed = formState {
            formState = .idle
        }
    }

    private func finishSignIn(email: String, password: String) async {
        do {
            let tokens = try await repository.logIn(email: email, password: password)
            await credentialStore.store(accessToken: tokens.accessToken, refreshToken: tokens.refreshToken)
            state = try await .signedIn(repository.currentUser())
            formState = .idle
            telemetry.record(event: "auth.signed_in")
        } catch {
            fail(error, context: "auth.sign_in")
        }
    }

    private func fail(_ error: APIError, context: String) {
        telemetry.record(error: error, context: context)
        formState = .failed(message: error.errorDescription ?? "Something went wrong. Please try again.")
    }
}
