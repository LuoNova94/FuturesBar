import XCTest
@testable import FuturesCore

final class ProxySettingsTests: XCTestCase {
    func testFreshInstallKeepsFollowingSystemAfterRelaunch() throws {
        try withDefaults { defaults in
            let settings = ProxySettings(defaults: defaults)
            XCTAssertFalse(settings.enabled)
            XCTAssertNil(settings.port)
            settings.save(to: defaults)
            defaults.set(true, forKey: "hasOpenedPanel")
            XCTAssertNil(ProxySettings(defaults: defaults).sessionPort)
        }
    }

    func testLegacyAndExplicitProxySettingsSurviveUpgrade() throws {
        try withDefaults { defaults in
            defaults.set(true, forKey: "hasOpenedPanel")
            let migrated = ProxySettings(defaults: defaults)
            XCTAssertEqual(migrated.sessionPort, 7892)
            migrated.save(to: defaults)
            let customized = try migrated.updated(enabled: true, portText: " 1087 ")
            customized.save(to: defaults)
            XCTAssertEqual(ProxySettings(defaults: defaults).sessionPort, 1087)
        }
    }

    func testFollowingSystemDoesNotRequirePortAndRemembersManualPort() throws {
        try withDefaults { defaults in
            let fresh = ProxySettings(defaults: defaults)
            XCTAssertNil(try fresh.updated(enabled: false, portText: "").sessionPort)
            let manual = try fresh.updated(enabled: true, portText: "7890")
            let system = try manual.updated(enabled: false, portText: "invalid draft")
            system.save(to: defaults)
            XCTAssertNil(ProxySettings(defaults: defaults).sessionPort)
            XCTAssertEqual(ProxySettings(defaults: defaults).port, 7890)
            for value in ["", "0", "65536", "abc"] {
                XCTAssertThrowsError(try fresh.updated(enabled: true, portText: value))
            }
        }
    }

    private func withDefaults(_ body: (UserDefaults) throws -> Void) throws {
        let name = "FuturesBar.ProxyTests.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: name))
        defer { defaults.removePersistentDomain(forName: name) }
        try body(defaults)
    }
}
