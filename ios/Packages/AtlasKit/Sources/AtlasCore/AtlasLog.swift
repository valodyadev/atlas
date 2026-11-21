import OSLog

/// Unified logging categories.
///
/// `Logger` writes to the system log, which means logs are captured on device
/// without a third-party SDK and can be pulled with `sysdiagnose`. Keep
/// messages free of personal data: anything interpolated without an explicit
/// privacy level is redacted in release builds by default, and we rely on that.
public enum AtlasLog {
    private static let subsystem = Bundle.main.bundleIdentifier ?? "com.atlas.app"

    /// App lifecycle, launch and configuration.
    public static let app = Logger(subsystem: subsystem, category: "app")

    /// HTTP requests and responses.
    public static let networking = Logger(subsystem: subsystem, category: "networking")

    /// Feature-level user-facing flows.
    public static let feature = Logger(subsystem: subsystem, category: "feature")

    /// Persistence and caching.
    public static let persistence = Logger(subsystem: subsystem, category: "persistence")
}
