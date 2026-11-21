import AtlasDesignSystem
import SwiftUI

/// The users list screen.
///
/// The reference feature screen: it owns no networking, renders every
/// `ViewState` case, and is previewable without a backend.
public struct UsersListView: View {
    @State private var viewModel: UsersListViewModel
    @State private var isPresentingCreateSheet = false

    public init(viewModel: UsersListViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    public var body: some View {
        content
            .navigationTitle("Users")
            .toolbar {
                ToolbarItem(placement: .primaryAction) {
                    Button {
                        isPresentingCreateSheet = true
                    } label: {
                        Label("Add user", systemImage: "plus")
                    }
                }
            }
            .sheet(isPresented: $isPresentingCreateSheet) {
                CreateUserView { email, displayName in
                    await viewModel.createUser(email: email, displayName: displayName)
                }
            }
            .task {
                await viewModel.load()
            }
            .refreshable {
                await viewModel.load()
            }
    }

    @ViewBuilder
    private var content: some View {
        switch viewModel.state {
        case .idle, .loading:
            AtlasLoadingView(message: "Loading users…")

        case .empty:
            AtlasEmptyStateView(
                title: "No users yet",
                message: "Users you add will appear here.",
                systemImage: "person.2",
                actionTitle: "Add the first user"
            ) {
                isPresentingCreateSheet = true
            }

        case let .failed(message):
            AtlasErrorStateView(message: message) {
                Task { await viewModel.load() }
            }

        case let .loaded(users):
            List {
                ForEach(users) { user in
                    UserRow(user: user)
                        .onAppear {
                            // Prefetch when the last row appears, so scrolling
                            // never stops at a spinner.
                            if user.id == users.last?.id {
                                Task { await viewModel.loadMore() }
                            }
                        }
                }

                if viewModel.isLoadingMore {
                    HStack {
                        Spacer()
                        ProgressView()
                        Spacer()
                    }
                }
            }
        }
    }
}

private struct UserRow: View {
    let user: User

    var body: some View {
        VStack(alignment: .leading, spacing: Atlas.Spacing.xxs) {
            Text(user.displayName)
                .font(Atlas.Typography.headline)
            Text(user.email)
                .font(Atlas.Typography.caption)
                .foregroundStyle(Atlas.Palette.secondaryText)
        }
        .padding(.vertical, Atlas.Spacing.xxs)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(user.displayName), \(user.email)")
    }
}
