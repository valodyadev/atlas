import SwiftUI

/// Shown while a screen's first load is in flight.
public struct AtlasLoadingView: View {
    private let message: String

    public init(message: String = "Loading…") {
        self.message = message
    }

    public var body: some View {
        VStack(spacing: Atlas.Spacing.md) {
            ProgressView()
            Text(message)
                .font(Atlas.Typography.caption)
                .foregroundStyle(Atlas.Palette.secondaryText)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(message)
    }
}

/// Shown when a screen loaded successfully but has nothing to display.
///
/// A distinct empty state (rather than a blank screen) is what tells the user
/// the app is working and what to do next.
public struct AtlasEmptyStateView: View {
    private let title: String
    private let message: String
    private let systemImage: String
    private let actionTitle: String?
    private let action: (() -> Void)?

    public init(
        title: String,
        message: String,
        systemImage: String = "tray",
        actionTitle: String? = nil,
        action: (() -> Void)? = nil
    ) {
        self.title = title
        self.message = message
        self.systemImage = systemImage
        self.actionTitle = actionTitle
        self.action = action
    }

    public var body: some View {
        ContentUnavailableView {
            Label(title, systemImage: systemImage)
        } description: {
            Text(message)
        } actions: {
            if let actionTitle, let action {
                Button(actionTitle, action: action)
                    .buttonStyle(.borderedProminent)
            }
        }
    }
}

/// Shown when a screen failed to load, with a way out.
///
/// Always offers a retry: an error the user cannot act on is a dead end.
public struct AtlasErrorStateView: View {
    private let message: String
    private let retry: () -> Void

    public init(message: String, retry: @escaping () -> Void) {
        self.message = message
        self.retry = retry
    }

    public var body: some View {
        ContentUnavailableView {
            Label("Something went wrong", systemImage: "exclamationmark.triangle")
        } description: {
            Text(message)
        } actions: {
            Button("Try again", action: retry)
                .buttonStyle(.borderedProminent)
        }
    }
}
