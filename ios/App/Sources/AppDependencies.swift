import AtlasCore
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

        let tokenStore = KeychainTokenStore()
        self.tokenStore = tokenStore

        apiClient = URLSessionAPIClient(
            configuration: APIConfiguration(
                baseURL: configuration.apiBaseURL,
                environment: configuration.environment
            ),
            tokenProvider: tokenStore,
            retryPolicy: RetryPolicy(),
            telemetry: telemetry
        )
    }

    // MARK: - Feature factories

    func makeUsersListViewModel() -> UsersListViewModel {
        UsersListViewModel(
            repository: LiveUsersRepository(client: apiClient),
            telemetry: telemetry
        )
    }
}
