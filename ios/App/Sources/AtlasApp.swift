import AtlasCore
import SwiftUI

/// Application entry point.
@main
struct AtlasApp: App {
    @State private var dependencies = AppDependencies()

    init() {
        AtlasLog.app.info("launching")
    }

    var body: some Scene {
        WindowGroup {
            AppRootView(dependencies: dependencies)
        }
    }
}
