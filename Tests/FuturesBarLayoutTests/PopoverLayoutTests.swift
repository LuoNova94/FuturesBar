import AppKit
import XCTest
@testable import FuturesBar

final class PopoverLayoutTests: XCTestCase {
    @MainActor
    func testContentStaysOnScreenAndBelowMenuBarDuringResize() throws {
        guard ProcessInfo.processInfo.environment["FUTURES_CHECK_LAYOUT"] == "1" else {
            throw XCTSkip("Run check-layout.sh in a macOS desktop session")
        }
        NSApplication.shared.setActivationPolicy(.accessory)
        NSApplication.shared.finishLaunching()
        let delegate = AppDelegate()
        delegate.checkingLayout = true
        delegate.applicationDidFinishLaunching(Notification(name: NSApplication.didFinishLaunchingNotification))
        delegate.checkSeedContracts()
        defer { delegate.applicationWillTerminate(Notification(name: NSApplication.willTerminateNotification)) }
        settle()
        delegate.checkShowPopover()
        settle(0.02)
        try assertVisible(delegate, context: "first rendered frame")
        settle()
        for iteration in 0..<5 {
            for (managing, network) in [(false, false), (true, false), (false, false), (false, true)] {
                NotificationCenter.default.post(name: Notification.Name("FuturesBar.CheckLayout"), object: nil,
                                                userInfo: ["managing": managing, "network": network])
                delegate.checkTitle(iteration % 2 == 0 ? "BTC 81,234  ·  ETH 2,456  ·  SOL 104.25" : "BTC —  ·  ETH —  ·  SOL —")
                settle()
                try assertVisible(delegate, context: "iteration=\(iteration) manage=\(managing) network=\(network)")
                if managing { XCTAssertGreaterThan(try XCTUnwrap(delegate.checkFrames).content.height, 700) }
                delegate.checkClosePopover()
                delegate.checkShowPopover()
                settle(0.02)
                try assertVisible(delegate, context: "first rendered frame after reopen; manage=\(managing)")
            }
        }
    }

    @MainActor
    private func assertVisible(_ delegate: AppDelegate, context: String) throws {
        let frames = try XCTUnwrap(delegate.checkFrames)
        let description = "\(context) window=\(frames.window) content=\(frames.content) screen=\(frames.screen) anchor=\(frames.anchor)"
        XCTAssertGreaterThanOrEqual(frames.content.minY, frames.screen.minY - 2, description)
        XCTAssertLessThanOrEqual(frames.content.maxY, frames.screen.maxY + 2, description)
        XCTAssertLessThanOrEqual(abs(frames.window.maxY - frames.anchor.minY), 24, description)
        XCTAssertGreaterThanOrEqual(frames.content.minX, frames.screen.minX - 2, description)
        XCTAssertLessThanOrEqual(frames.content.maxX, frames.screen.maxX + 2, description)
    }

    @MainActor
    private func settle(_ seconds: TimeInterval = 0.25) {
        RunLoop.main.run(until: Date().addingTimeInterval(seconds))
    }
}
