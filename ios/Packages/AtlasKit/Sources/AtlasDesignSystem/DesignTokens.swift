import SwiftUI

/// The design system's primitives.
///
/// Features reference tokens, never raw numbers or colours. That is what makes
/// a spacing or palette change a one-file diff instead of a scavenger hunt,
/// and it is what keeps twelve screens built by six people looking like one app.
public enum Atlas {
    /// A 4-point spacing scale. Anything not on the scale is a bug.
    public enum Spacing {
        public static let xxs: CGFloat = 2
        public static let xs: CGFloat = 4
        public static let sm: CGFloat = 8
        public static let md: CGFloat = 12
        public static let lg: CGFloat = 16
        public static let xl: CGFloat = 24
        public static let xxl: CGFloat = 32
    }

    /// Corner radii.
    public enum Radius {
        public static let sm: CGFloat = 6
        public static let md: CGFloat = 10
        public static let lg: CGFloat = 16
    }

    /// Semantic colours.
    ///
    /// Built on the system palette so Dark Mode, Increase Contrast and
    /// system-wide tint all work without per-screen handling.
    public enum Palette {
        public static let accent = Color.accentColor
        public static let primaryText = Color.primary
        public static let secondaryText = Color.secondary
        public static let destructive = Color.red
        public static let separator = Color.gray.opacity(0.25)
    }

    /// Text styles.
    ///
    /// All derived from Dynamic Type styles, never fixed point sizes, so the
    /// app respects the user's text-size setting. This is an accessibility
    /// requirement, not a preference.
    public enum Typography {
        public static let title = Font.title2.weight(.semibold)
        public static let headline = Font.headline
        public static let body = Font.body
        public static let caption = Font.caption
    }

    /// Minimum tappable size, per Apple's Human Interface Guidelines.
    public static let minimumTapTarget: CGFloat = 44
}
