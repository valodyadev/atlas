import AtlasCore
import Foundation

/// Launch-time configuration, read from the active build configuration.
///
/// Values arrive via `.xcconfig` -> `Info.plist` -> here, so Debug, Staging
/// and Release builds differ by build setting rather than by `#if` branches
/// scattered through the source. One source file, three behaviours, and a
/// TestFlight build can point at staging without a code change.
struct AppConfiguration: Sendable {
    let environment: AppEnvironmentKind
    let apiBaseURL: URL

    /// Reads configuration from the main bundle.
    ///
    /// Deliberately fatal on bad input: an app that launches with no API URL
    /// can only fail later, in a confusing way, in front of a user.
    static func fromBundle(_ bundle: Bundle = .main) -> AppConfiguration {
        guard let rawEnvironment = bundle.object(forInfoDictionaryKey: "ATLAS_ENVIRONMENT") as? String,
              let environment = AppEnvironmentKind(rawValue: rawEnvironment)
        else {
            fatalError("ATLAS_ENVIRONMENT is missing or invalid. Check the active .xcconfig.")
        }

        guard let rawURL = bundle.object(forInfoDictionaryKey: "ATLAS_API_BASE_URL") as? String,
              let url = URL(string: rawURL)
        else {
            fatalError("ATLAS_API_BASE_URL is missing or invalid. Check the active .xcconfig.")
        }

        return AppConfiguration(environment: environment, apiBaseURL: url)
    }
}
