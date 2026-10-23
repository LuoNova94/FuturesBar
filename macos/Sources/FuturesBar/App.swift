import AppKit
import Combine
import FuturesCore
import SwiftUI

@main
struct FuturesBarMain {
    @MainActor
    static func main() {
        let app = NSApplication.shared
        let delegate = AppDelegate()
        app.delegate = delegate
        app.setActivationPolicy(.accessory)
        withExtendedLifetime(delegate) { app.run() }
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate, NSPopoverDelegate {
    private let store = MarketStore()
    private var statusItem: NSStatusItem!
    private let popover = NSPopover()
    private var subscriptions = Set<AnyCancellable>()
    private var observers: [NSObjectProtocol] = []
    #if DEBUG
    var checkingLayout = false
    #endif

    func applicationDidFinishLaunching(_ notification: Notification) {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        statusItem.button?.target = self
        statusItem.button?.action = #selector(togglePopover)
        statusItem.button?.font = .monospacedDigitSystemFont(ofSize: 12, weight: .medium)
        statusItem.button?.setAccessibilityLabel("合约行情，点击管理最多三个品种")
        popover.behavior = .transient
        popover.animates = false
        popover.delegate = self
        let hosting = NSHostingController(rootView: Dashboard(store: store))
        // NSPopover must receive ideal-size changes instead of allowing its
        // hosting view to resize the window independently around a stale origin.
        hosting.sizingOptions = [.preferredContentSize]
        popover.contentViewController = hosting
        store.objectWillChange.sink { [weak self] _ in
            DispatchQueue.main.async { self?.updateTitle() }
        }.store(in: &subscriptions)
        let center = NSWorkspace.shared.notificationCenter
        observers.append(center.addObserver(forName: NSWorkspace.willSleepNotification, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.store.sleep() }
        })
        observers.append(center.addObserver(forName: NSWorkspace.didWakeNotification, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.store.wake() }
        })
        updateTitle()
        #if DEBUG
        if checkingLayout { return }
        #endif
        store.start()
        if !UserDefaults.standard.bool(forKey: "hasOpenedPanel") {
            UserDefaults.standard.set(true, forKey: "hasOpenedPanel")
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.4) { [weak self] in self?.togglePopover() }
        }
    }

    private func updateTitle() {
        let pieces = store.watchlist.symbols.map { symbol -> String in
            let asset = store.contract(symbol).baseAsset
            guard let quote = store.quotes[symbol], quote.isFresh(at: store.now) else { return "\(asset) —" }
            return "\(asset) \(PriceText.format(quote.price, compact: store.compact))"
        }
        statusItem.button?.title = pieces.joined(separator: "  ·  ")
        statusItem.button?.appearsDisabled = !store.allFresh
        statusItem.button?.toolTip = "Binance · USDT 永续 · \(store.kind.title)\n\(store.allFresh ? "实时连接" : store.status)"
    }

    @objc private func togglePopover() {
        guard let button = statusItem.button else { return }
        if popover.isShown { popover.performClose(nil) }
        else {
            store.refreshLoginStatus()
            popover.contentViewController?.view.layoutSubtreeIfNeeded()
            if let size = popover.contentViewController?.view.fittingSize {
                popover.contentSize = size
            }
            popover.show(relativeTo: button.bounds, of: button, preferredEdge: .minY)
            NSApp.activate(ignoringOtherApps: true)
            popover.contentViewController?.view.window?.makeKey()
        }
    }

    func applicationWillTerminate(_ notification: Notification) {
        store.stop()
        popover.performClose(nil)
        if let statusItem { NSStatusBar.system.removeStatusItem(statusItem) }
        observers.forEach { NSWorkspace.shared.notificationCenter.removeObserver($0) }
    }

    #if DEBUG
    func checkShowPopover() {
        if !popover.isShown { togglePopover() }
    }

    func checkClosePopover() { popover.performClose(nil) }
    func checkSeedContracts() { store.checkSeedContracts() }

    func checkTitle(_ title: String) { statusItem.button?.title = title }

    var checkFrames: (window: NSRect, content: NSRect, screen: NSRect, anchor: NSRect)? {
        guard let view = popover.contentViewController?.view,
              let window = view.window, let button = statusItem.button,
              let anchorWindow = button.window, let screen = anchorWindow.screen else { return nil }
        return (window.frame, window.convertToScreen(view.convert(view.bounds, to: nil)),
                screen.visibleFrame, anchorWindow.convertToScreen(button.convert(button.bounds, to: nil)))
    }
    #endif
}
