import AtlasDesignSystem
import SwiftUI

/// Sheet for registering a user.
struct CreateUserView: View {
    /// Returns `true` when the user was created.
    let onSubmit: (String, String) async -> Bool

    @Environment(\.dismiss) private var dismiss
    @State private var email = ""
    @State private var displayName = ""
    @State private var isSubmitting = false

    private var canSubmit: Bool {
        !isSubmitting
            && email.contains("@")
            && !displayName.trimmingCharacters(in: .whitespaces).isEmpty
    }

    var body: some View {
        NavigationStack {
            Form {
                Section("Details") {
                    TextField("Email", text: $email)
                        #if os(iOS)
                        .textInputAutocapitalization(.never)
                        .keyboardType(.emailAddress)
                        #endif
                        .textContentType(.emailAddress)
                        .autocorrectionDisabled()

                    TextField("Display name", text: $displayName)
                        .textContentType(.name)
                }

                Section {
                    Button {
                        Task { await submit() }
                    } label: {
                        if isSubmitting {
                            ProgressView()
                        } else {
                            Text("Create user")
                        }
                    }
                    .buttonStyle(.atlasPrimary)
                    .disabled(!canSubmit)
                }
            }
            .navigationTitle("New user")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
            }
        }
    }

    private func submit() async {
        isSubmitting = true
        defer { isSubmitting = false }

        if await onSubmit(email, displayName) {
            dismiss()
        }
    }
}
