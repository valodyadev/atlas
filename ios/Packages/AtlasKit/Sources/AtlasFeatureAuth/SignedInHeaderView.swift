import AtlasDesignSystem
import SwiftUI

/// A compact account summary with a way out.
///
/// Signing out is deliberately easy to find. Hiding it is a dark pattern, and
/// people need it to switch accounts during testing.
public struct SignedInHeaderView: View {
    private let user: CurrentUser
    private let signOut: () async -> Void

    @State private var isSigningOut = false

    public init(user: CurrentUser, signOut: @escaping () async -> Void) {
        self.user = user
        self.signOut = signOut
    }

    public var body: some View {
        List {
            Section("Signed in as") {
                LabeledContent("Name", value: user.displayName)
                LabeledContent("Email", value: user.email)
                LabeledContent("Role", value: user.role.capitalized)
            }

            Section {
                Button(role: .destructive) {
                    isSigningOut = true
                    Task {
                        await signOut()
                        isSigningOut = false
                    }
                } label: {
                    if isSigningOut {
                        ProgressView()
                    } else {
                        Text("Sign out")
                    }
                }
                .disabled(isSigningOut)
            }
        }
        .accessibilityLabel("Account")
    }
}
