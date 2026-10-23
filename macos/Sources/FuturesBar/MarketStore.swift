import AppKit
import Combine
import Foundation
import FuturesCore
import ServiceManagement

@MainActor
final class MarketStore: ObservableObject {
    @Published private(set) var watchlist: Watchlist
    @Published private(set) var contracts: [Contract] = []
    @Published private(set) var quotes: [String: Quote] = [:]
    @Published private(set) var status = "正在连接行情…"
    @Published private(set) var catalogError: String?
    @Published private(set) var loadingCatalog = false
    @Published private(set) var now = Date()
    @Published var kind: PriceKind { didSet {
        guard kind != oldValue else { return }
        defaults.set(kind.rawValue, forKey: "priceKind")
        restart()
    } }
    @Published var compact: Bool { didSet { defaults.set(compact, forKey: "compact") } }
    @Published private(set) var loginEnabled = false
    @Published var loginMessage: String?
    @Published private(set) var useProxy: Bool
    @Published private(set) var proxyPort: Int?
    @Published var proxyMessage: String?

    private let defaults = UserDefaults.standard
    private var session: URLSession
    private var connection: Task<Void, Never>?
    private var catalogTask: Task<Void, Never>?
    private var clockTask: Task<Void, Never>?
    private var socket: URLSessionWebSocketTask?
    private var generation = UUID()
    private var catalogGeneration = UUID()
    private var sleeping = false
    private var clockStartedAt = Date()

    init() {
        watchlist = Watchlist(UserDefaults.standard.stringArray(forKey: "symbols") ?? ["BTCUSDT", "ETHUSDT", "SOLUSDT"])
        kind = PriceKind(rawValue: UserDefaults.standard.string(forKey: "priceKind") ?? "") ?? .last
        compact = UserDefaults.standard.object(forKey: "compact") as? Bool ?? true
        let settings = ProxySettings(defaults: UserDefaults.standard)
        settings.save(to: UserDefaults.standard)
        useProxy = settings.enabled
        proxyPort = settings.port
        session = BinanceAPI.makeSession(proxyPort: settings.sessionPort)
        refreshLoginStatus()
    }

    var allFresh: Bool {
        watchlist.symbols.allSatisfy { quotes[$0]?.isFresh(at: now) == true }
    }

    func start() {
        loadContracts()
        restart()
        clockTask = Task { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(nanoseconds: 1_000_000_000)
                guard !Task.isCancelled, let self else { return }
                self.now = Date()
                guard !self.sleeping else { continue }
                let elapsed = self.now.timeIntervalSince(self.clockStartedAt)
                let received = self.quotes.values.map(\.eventTime).max() ?? self.clockStartedAt
                if elapsed > 25 && self.now.timeIntervalSince(received) > 25 {
                    self.restart()
                }
            }
        }
    }

    func contract(_ symbol: String) -> Contract {
        contracts.first { $0.symbol == symbol } ?? .placeholder(symbol)
    }

    func toggle(_ symbol: String) {
        if !watchlist.symbols.contains(symbol) {
            guard contracts.contains(where: { $0.symbol == symbol && $0.isSupported }) else { return }
        }
        guard watchlist.toggle(symbol) else { return }
        saveSelection()
        restart()
    }

    func move(_ symbol: String, by offset: Int) {
        watchlist.move(symbol, by: offset)
        saveSelection()
    }

    private func saveSelection() { defaults.set(watchlist.symbols, forKey: "symbols") }

    func applyProxy(enabled: Bool, portText: String) {
        let settings: ProxySettings
        do {
            settings = try ProxySettings(defaults: defaults).updated(enabled: enabled, portText: portText)
        } catch {
            proxyMessage = error.localizedDescription
            return
        }
        proxyMessage = nil
        useProxy = enabled
        proxyPort = settings.port
        settings.save(to: defaults)
        connection?.cancel()
        socket?.cancel(with: .goingAway, reason: nil)
        catalogTask?.cancel()
        catalogGeneration = UUID()
        loadingCatalog = false
        session.invalidateAndCancel()
        session = BinanceAPI.makeSession(proxyPort: settings.sessionPort)
        loadContracts()
        restart()
    }

    func loadContracts() {
        guard !loadingCatalog else { return }
        loadingCatalog = true
        catalogError = nil
        let token = UUID()
        catalogGeneration = token
        catalogTask = Task { [weak self] in
            guard let self else { return }
            defer { if self.catalogGeneration == token { self.loadingCatalog = false } }
            do {
                let values = try await BinanceAPI.contracts(session: self.session)
                guard !Task.isCancelled, self.catalogGeneration == token else { return }
                self.contracts = values
                if values.isEmpty { self.catalogError = "未获取到可用合约，请稍后重试。" }
            } catch {
                guard !Task.isCancelled, self.catalogGeneration == token else { return }
                self.catalogError = "无法加载品种列表：\(error.localizedDescription)"
            }
        }
    }

    func restart() {
        connection?.cancel()
        socket?.cancel(with: .goingAway, reason: nil)
        socket = nil
        quotes = [:]
        generation = UUID()
        clockStartedAt = Date()
        guard !sleeping else { status = "Mac 休眠中"; return }
        status = "正在连接行情…"
        let token = generation
        let symbols = watchlist.symbols
        let priceKind = kind
        connection = Task { [weak self] in
            var failures = 0
            while !Task.isCancelled {
                guard let self, self.generation == token else { return }
                let ws = self.session.webSocketTask(with: BinanceAPI.streamURL(symbols: symbols, kind: priceKind))
                self.socket = ws
                ws.resume()
                do {
                    while !Task.isCancelled {
                        let message = try await ws.receive()
                        guard !Task.isCancelled, self.generation == token else { return }
                        let quote = try Quote.decode(BinanceAPI.messageData(message), kind: priceKind)
                        guard symbols.contains(quote.symbol) else { continue }
                        // Ignore delayed/out-of-order packets from the feed.
                        if let existing = self.quotes[quote.symbol], existing.eventTime > quote.eventTime { continue }
                        self.quotes[quote.symbol] = quote
                        self.status = "实时连接"
                        failures = 0
                    }
                } catch {
                    ws.cancel(with: .goingAway, reason: nil)
                    guard !Task.isCancelled, self.generation == token else { return }
                    failures += 1
                    let delay = min(30, 1 << min(failures, 5))
                    self.quotes = [:]
                    self.status = "连接中断，\(delay) 秒后重试"
                    do { try await Task.sleep(nanoseconds: UInt64(delay) * 1_000_000_000) }
                    catch { return }
                }
            }
        }
    }

    func sleep() {
        sleeping = true
        restart()
    }

    func wake() {
        sleeping = false
        restart()
        if contracts.isEmpty { loadContracts() }
    }

    func refreshLoginStatus() { loginEnabled = SMAppService.mainApp.status == .enabled }

    func setLogin(_ enabled: Bool) {
        do {
            if enabled { try SMAppService.mainApp.register() }
            else { try SMAppService.mainApp.unregister() }
            refreshLoginStatus()
            loginMessage = SMAppService.mainApp.status == .requiresApproval ? "请在系统设置 → 通用 → 登录项中允许启动。" : nil
        } catch {
            refreshLoginStatus()
            loginMessage = "无法更改开机启动：\(error.localizedDescription)"
        }
    }

    func stop() {
        connection?.cancel()
        catalogTask?.cancel()
        clockTask?.cancel()
        socket?.cancel(with: .goingAway, reason: nil)
        session.invalidateAndCancel()
    }

    #if DEBUG
    func checkSeedContracts() {
        contracts = ["BTC", "ETH", "SOL", "BNB", "DOGE", "XRP", "1000SHIB", "1000XEC"].map {
            Contract(symbol: "\($0)USDT", baseAsset: $0)
        }
    }
    #endif
}
