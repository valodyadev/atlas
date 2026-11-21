import AtlasCore
import AtlasDesignSystem
import AtlasFeatureAuth
import AtlasFeatureUsers
import SwiftUI

/// Top-level navigation.
///
/// A `TabView` of feature entry points: each tab owns a `NavigationStack`, so
/// features never reach into each other's navigation state. Adding a feature
/// means adding a tab and a factory call, nothing more.
struct AppRootView: View {
    let dependencies: AppDependencies

    var body: some View {
        switch dependencies.authStore.state {
        case .restoring:
            // A brief, deliberate blank rather than flashing the sign-in
            // screen at someone who is already signed in.
            ProgressView("Signing you in…")
                .task { await dependencies.authStore.restore() }

        case .signedOut:
            AuthView(store: dependencies.authStore)

        case let .signedIn(user):
            signedIn(user: user)
        }
    }

    private func signedIn(user: CurrentUser) -> some View {
        TabView {
            Tab("Users", systemImage: "person.2") {
                NavigationStack {
                    UsersListView(viewModel: dependencies.makeUsersListViewModel())
                }
            }

            Tab("Account", systemImage: "person.crop.circle") {
                NavigationStack {
                    SignedInHeaderView(user: user) {
                        await dependencies.authStore.signOut()
                    }
                    .navigationTitle("Account")
                }
            }

            Tab("About", systemImage: "info.circle") {
                NavigationStack {
                    AboutView(configuration: dependencies.configuration)
                }
            }
        }
    }
}

/// Build and environment information.
///
/// Useful well beyond vanity: when a tester reports a bug, this is how you
/// find out which build and which backend they were actually on.
private struct AboutView: View {
    let configuration: AppConfiguration

    private var version: String {
        let short = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "?"
        let build = Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? "?"
        return "\(short) (\(build))"
    }

    var body: some View {
        List {
            Section("Build") {
                LabeledContent("Version", value: version)
                LabeledContent("Environment", value: configuration.environment.rawValue.capitalized)
            }

            Section("Backend") {
                LabeledContent("API", value: configuration.apiBaseURL.absoluteString)
                    .font(Atlas.Typography.caption)
                    .textSelection(.enabled)
            }
        }
        .navigationTitle("About")
    }
}
