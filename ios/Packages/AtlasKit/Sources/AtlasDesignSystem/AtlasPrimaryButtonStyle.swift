import SwiftUI

/// The app's primary call to action.
public struct AtlasPrimaryButtonStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled

    public init() {}

    public func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(Atlas.Typography.headline)
            .frame(minHeight: Atlas.minimumTapTarget)
            .frame(maxWidth: .infinity)
            .padding(.horizontal, Atlas.Spacing.lg)
            .background(
                RoundedRectangle(cornerRadius: Atlas.Radius.md, style: .continuous)
                    .fill(Atlas.Palette.accent.opacity(isEnabled ? 1 : 0.4))
            )
            .foregroundStyle(.white)
            .opacity(configuration.isPressed ? 0.8 : 1)
            .animation(.easeOut(duration: 0.12), value: configuration.isPressed)
    }
}

public extension ButtonStyle where Self == AtlasPrimaryButtonStyle {
    /// The app's primary call to action.
    static var atlasPrimary: AtlasPrimaryButtonStyle { AtlasPrimaryButtonStyle() }
}
