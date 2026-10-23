import Foundation
import FuturesCore

@main
struct FuturesProbe {
    static func main() async {
        do {
            let port = ProcessInfo.processInfo.environment["FUTURES_PROXY_PORT"].flatMap(Int.init)
            let session = BinanceAPI.makeSession(proxyPort: port)
            defer { session.invalidateAndCancel() }
            let contracts = try await BinanceAPI.contracts(session: session)
            let symbols = ProcessInfo.processInfo.environment["FUTURES_TEST_SYMBOLS"]?.split(separator: ",").map { String($0).uppercased() } ?? ["BTCUSDT", "ETHUSDT", "SOLUSDT"]
            guard !symbols.isEmpty, symbols.count <= 3, symbols.allSatisfy({ symbol in contracts.contains { $0.symbol == symbol } }) else { throw MarketError.invalidQuote }
            print("REST OK: \(contracts.count) 个可交易的 USDT 永续合约")
            for kind in PriceKind.allCases {
                let quotes = try await probe(kind, symbols: symbols, session: session)
                print("WebSocket \(kind.title) OK: " + quotes.sorted { $0.symbol < $1.symbol }.map {
                    "\($0.symbol)=\(PriceText.format($0.price, compact: false))"
                }.joined(separator: ", "))
            }
        } catch {
            fputs("行情验证失败：\(error.localizedDescription)\n", stderr)
            exit(1)
        }
    }

    static func probe(_ kind: PriceKind, symbols: [String], session: URLSession) async throws -> [Quote] {
        let socket = session.webSocketTask(with: BinanceAPI.streamURL(symbols: symbols, kind: kind))
        socket.resume()
        defer { socket.cancel(with: .goingAway, reason: nil) }
        return try await withThrowingTaskGroup(of: [Quote].self) { group in
            group.addTask {
                var received: [String: Quote] = [:]
                while received.count < symbols.count {
                    let message = try await socket.receive()
                    let quote = try Quote.decode(BinanceAPI.messageData(message), kind: kind)
                    if symbols.contains(quote.symbol), quote.isFresh() { received[quote.symbol] = quote }
                }
                return Array(received.values)
            }
            group.addTask {
                try await Task.sleep(nanoseconds: 20_000_000_000)
                socket.cancel(with: .goingAway, reason: nil)
                throw MarketError.timeout
            }
            defer { group.cancelAll() }
            return try await group.next()!
        }
    }
}
