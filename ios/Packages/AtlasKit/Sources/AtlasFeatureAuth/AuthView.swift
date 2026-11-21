import AtlasDesignSystem
import SwiftUI

/// The sign-in and create-account screen.
///
/// One decision per screen, the keyboard correct on first tap, errors stated
/// inline and specifically, and never a dead end — the structure that makes a
/// consumer sign-in flow feel effortless.
public struct AuthView: View {
    @Bindable private var store: AuthStore

    @State private var mode: AuthMode = .signIn
    @State private var email = ""
    @State private var password = ""
    @State private var displayName = ""
    @FocusState private var focused: Field?

    private enum Field: Hashable { case email, password, displayName }

    public init(store: AuthStore) {
        self.store = store
    }

    private var isWorking: Bool {
        if case .working = store.formState {
            return true
        }
        return false
    }

    private var errorMessage: String? {
        if case let .failed(message) = store.formState {
            return message
        }
        return nil
    }

    /// Mirrors the server's rule rather than inventing a stricter one.
    private var passwordIsLongEnough: Bool { password.count >= 12 }
    private var emailLooksValid: Bool { email.contains("@") && !email.hasPrefix("@") && !email.hasSuffix("@") }
    private var nameIsPresent: Bool {
        mode == .signIn || !displayName.trimmingCharacters(in: .whitespaces).isEmpty
    }

    private var canSubmit: Bool {
        !isWorking && emailLooksValid && passwordIsLongEnough && nameIsPresent
    }

    public var body: some View {
        NavigationStack {
            Form {
                Section {
                    Picker("What would you like to do?", selection: $mode) {
                        ForEach(AuthMode.allCases, id: \.self) { option in
                            Text(option.title).tag(option)
                        }
                    }
                    .pickerStyle(.segmented)
                    .onChange(of: mode) { _, _ in store.clearError() }
                }

                Section {
                    if mode == .createAccount {
                        TextField("Name", text: $displayName)
                            .textContentType(.name)
                            .focused($focused, equals: .displayName)
                            .submitLabel(.next)
                            .onSubmit { focused = .email }
                            .accessibilityLabel("Your name")
                    }

                    TextField("Email", text: $email)
                        #if os(iOS)
                        .textInputAutocapitalization(.never)
                        .keyboardType(.emailAddress)
                        #endif
                        .textContentType(.username)
                        .autocorrectionDisabled()
                        .focused($focused, equals: .email)
                        .submitLabel(.next)
                        .onSubmit { focused = .password }
                        .onChange(of: email) { _, _ in store.clearError() }
                        .accessibilityLabel("Email address")

                    SecureField("Password", text: $password)
                        // `.newPassword` tells the password manager to offer a
                        // strong one; `.password` tells it to fill the saved one.
                        .textContentType(mode == .createAccount ? .newPassword : .password)
                        .focused($focused, equals: .password)
                        .submitLabel(.go)
                        .onSubmit {
                            if canSubmit {
                                submit()
                            }
                        }
                        .onChange(of: password) { _, _ in store.clearError() }
                        .accessibilityLabel("Password")
                } footer: {
                    if mode == .createAccount {
                        Text("At least 12 characters. A memorable phrase beats a short complex one.")
                            .font(Atlas.Typography.caption)
                            // Announced by VoiceOver rather than relying on
                            // someone noticing small grey text.
                            .accessibilityLabel("Password must be at least 12 characters.")
                    }
                }

                if let errorMessage {
                    Section {
                        Label(errorMessage, systemImage: "exclamationmark.circle")
                            .foregroundStyle(Atlas.Palette.destructive)
                            .accessibilityAddTraits(.isStaticText)
                    }
                }

                Section {
                    Button(action: submit) {
                        if isWorking {
                            ProgressView().frame(maxWidth: .infinity)
                        } else {
                            Text(mode.title)
                        }
                    }
                    .buttonStyle(.atlasPrimary)
                    .disabled(!canSubmit)
                    .accessibilityHint(canSubmit
                        ? "Submits the form"
                        : "Fill in every field to continue")
                }
            }
            .navigationTitle(mode == .signIn ? "Welcome back" : "Create your account")
            .disabled(isWorking)
        }
    }

    private func submit() {
        focused = nil
        Task {
            switch mode {
            case .signIn:
                await store.signIn(email: email, password: password)
            case .createAccount:
                await store.createAccount(email: email, password: password, displayName: displayName)
            }
        }
    }
}
