import Foundation
import Testing
@testable import AtlasCore

@Suite("App environment")
struct AppEnvironmentKindTests {
    @Test("Only production suppresses diagnostics")
    func diagnosticsGating() {
        #expect(AppEnvironmentKind.development.allowsDiagnostics)
        #expect(AppEnvironmentKind.staging.allowsDiagnostics)
        #expect(!AppEnvironmentKind.production.allowsDiagnostics)
    }

    @Test("Raw values are stable, because build settings depend on them")
    func rawValuesAreStable() {
        #expect(AppEnvironmentKind(rawValue: "development") == .development)
        #expect(AppEnvironmentKind(rawValue: "staging") == .staging)
        #expect(AppEnvironmentKind(rawValue: "production") == .production)
        #expect(AppEnvironmentKind(rawValue: "prod") == nil)
    }
}

@Suite("Date provider")
struct DateProvidingTests {
    @Test("A fixed provider does not move")
    func fixedProviderIsStable() {
        let instant = Date(timeIntervalSince1970: 1_800_000_000)
        let provider = FixedDateProvider(now: instant)

        #expect(provider.now == instant)
        #expect(provider.now == instant)
    }
}

/// Records what it is given, so tests can assert on telemetry without a vendor SDK.
final class RecordingTelemetry: Telemetry, @unchecked Sendable {
    private let lock = NSLock()
    private var _events: [String] = []

    var events: [String] {
        lock.withLock { _events }
    }

    func record(event name: String, metadata: [String: String]) {
        lock.withLock { _events.append(name) }
    }

    func record(error: Error, context: String) {
        lock.withLock { _events.append("error:\(context)") }
    }
}

@Suite("Telemetry")
struct TelemetryTests {
    @Test("The convenience overload forwards to the metadata overload")
    func convenienceOverloadForwards() {
        let telemetry = RecordingTelemetry()

        telemetry.record(event: "screen.viewed")

        #expect(telemetry.events == ["screen.viewed"])
    }

    @Test("The no-op sink swallows everything")
    func noopSwallows() {
        let telemetry = NoopTelemetry()
        telemetry.record(event: "ignored")
        telemetry.record(error: URLError(.notConnectedToInternet), context: "ignored")
    }
}
