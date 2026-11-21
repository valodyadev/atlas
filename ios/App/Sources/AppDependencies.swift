import AtlasCore
import AtlasFeatureAuth
import AtlasFeatureUsers
import AtlasNetworking
import Foundation

/// The composition root.
///
/// Everything is constructed here, once, and passed down. There is no service
/// locator and no singleton registry: the dependency graph is this file, so
/// "what does this screen need?" is answered by reading it rather than by
/// tracing registrations at runtime. When this file gets long, that is the
/// signal to introduce per-feature factories, not a DI container.
@MainActor
final class AppDependencies {
    let configuration: AppConfiguration
    let telemetry: any Telemetry
    let apiClient: any APIClient
    let tokenStore: KeychainTokenStore

    init(configuration: AppConfiguration = .fromBundle()) {
        self.configuration = configuration

        // Swap this for a vendor adapter (Sentry, Datadog, Firebase) by
        // conforming it to `Telemetry`; nothing else in the app changes.
        telemetry = configuration.environment.allowsDiagnostics
            ? OSLogTelemetry()
            : NoopTelemetry()

        // Order matters. The refresh client is unauthenticated and never
        // retries, so it can be built first and handed to the token store at
        // construction — no window in which a refresh could fire before the
        // handler exists, and no recursion if a refresh itself 401s.
        let apiConfiguration = APIConfiguration(
            baseURL: configuration.apiBaseURL,
            environment: configuration.environment
        )

        let refreshClient = URLSessionAPIClient(
            configuration: apiConfiguration,
            tokenProvider: AnonymousTokenProvider(),
            retryPolicy: .none,
            telemetry: telemetry
        )

        let tokenStore = KeychainTokenStore(refreshHandler: { refreshToken in
            let endpoint: Endpoint
            do {
                endpoint = try Endpoint.json(
                    method: .post,
                    path: "auth/refresh",
                    body: ["refreshToken": refreshToken],
                    requiresAuthentication: false
                )
            } catch {
                return nil
            }
            return try? await refreshClient.send(endpoint, as: AuthTokens.self)
        })
        self.tokenStore = tokenStore

        apiClient = URLSessionAPIClient(
            configuration: apiConfiguration,
            tokenProvider: tokenStore,
            retryPolicy: RetryPolicy(),
            telemetry: telemetry
        )

        authStore = AuthStore(
            repository: LiveAuthRepository(client: apiClient),
            credentialStore: tokenStore,
            telemetry: telemetry
        )
    }

    /// The session. The root view switches on this.
    let authStore: AuthStore

    // MARK: - Feature factories

    func makeUsersListViewModel() -> UsersListViewModel {
        UsersListViewModel(
            repository: LiveUsersRepository(client: apiClient),
            telemetry: telemetry
        )
    }
}
