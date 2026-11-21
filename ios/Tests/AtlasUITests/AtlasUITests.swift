import XCTest

/// End-to-end UI tests.
///
/// Deliberately thin. UI tests are the slowest and flakiest tier, so they are
/// reserved for a handful of critical journeys - launch, sign-in, the primary
/// task - while everything else is covered by the fast unit tests in
/// `Packages/AtlasKit`. Growing this file is usually a sign that a view model
/// test is missing.
///
/// XCTest rather than swift-testing: UI automation still runs on XCUITest.
final class AtlasUITests: XCTestCase {
    override func setUp() {
        super.setUp()
        continueAfterFailure = false
    }

    func testAppLaunchesAndShowsTheUsersTab() {
        let app = XCUIApplication()
        app.launch()

        XCTAssertTrue(
            app.tabBars.buttons["Users"].waitForExistence(timeout: 10),
            "The Users tab should be visible on launch"
        )
    }

    func testAboutTabReportsTheEnvironment() {
        let app = XCUIApplication()
        app.launch()

        app.tabBars.buttons["About"].tap()

        XCTAssertTrue(
            app.staticTexts["Environment"].waitForExistence(timeout: 5),
            "The About screen should report which backend the build points at"
        )
    }
}
