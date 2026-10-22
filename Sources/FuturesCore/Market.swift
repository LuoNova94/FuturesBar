import Foundation
import CFNetwork

public enum PriceKind: String, CaseIterable, Codable {
    case last, mark
    public var title: String { self == .last ? "最新成交价" : "标记价格" }
    public var stream: String { self == .last ? "ticker" : "markPrice@1s" }
}

public struct Contract: Codable, Identifiable, Equatable {
    public let symbol: String
    public let baseAsset: String
    public let quoteAsset: String
    public let contractType: String
    public let status: String
    public var id: String { symbol }
    public var isSupported: Bool {
        quoteAsset == "USDT" && contractType == "PERPETUAL" && status == "TRADING"
    }

    public init(symbol: String, baseAsset: String, quoteAsset: String = "USDT",
                contractType: String = "PERPETUAL", status: String = "TRADING") {
        self.symbol = symbol
        self.baseAsset = baseAsset
        self.quoteAsset = quoteAsset
        self.contractType = contractType
        self.status = status
    }

    // Used only to name saved selections before exchange information arrives.
    public static func placeholder(_ symbol: String) -> Contract {
        Contract(symbol: symbol, baseAsset: symbol.hasSuffix("USDT") ? String(symbol.dropLast(4)) : symbol)
    }
}

public struct Watchlist: Equatable {
    public static let limit = 3
    public private(set) var symbols: [String]

    public init(_ symbols: [String]) {
        var seen = Set<String>()
        self.symbols = Array(symbols.map { $0.uppercased() }.filter {
            !$0.isEmpty && $0.allSatisfy { $0.isASCII && ($0.isLetter || $0.isNumber) }
                && seen.insert($0).inserted
        }.prefix(Self.limit))
        if self.symbols.isEmpty { self.symbols = ["BTCUSDT"] }
    }

    @discardableResult
    public mutating func toggle(_ symbol: String) -> Bool {
        if symbols.contains(symbol) {
            guard symbols.count > 1 else { return false }
            symbols.removeAll { $0 == symbol }
        } else {
            guard symbols.count < Self.limit,
                  Watchlist([symbol]).symbols == [symbol] else { return false }
            symbols.append(symbol)
        }
        return true
    }

    public mutating func move(_ symbol: String, by offset: Int) {
        guard let index = symbols.firstIndex(of: symbol), symbols.indices.contains(index + offset) else { return }
        symbols.swapAt(index, index + offset)
    }
}

public struct Quote: Equatable {
    public let symbol: String
    public let price: Decimal
    public let changePercent: Decimal?
    public let eventTime: Date

    public func isFresh(at date: Date = Date()) -> Bool {
        let age = date.timeIntervalSince(eventTime)
        return age >= -10 && age < 15
    }

    public static func decode(_ data: Data, kind: PriceKind) throws -> Quote {
        let decoder = JSONDecoder()
        let frame: Frame
        if let combined = try? decoder.decode(Combined.self, from: data) {
            frame = combined.data
        } else {
            frame = try decoder.decode(Frame.self, from: data)
        }
        let expected = kind == .last ? "24hrTicker" : "markPriceUpdate"
        guard frame.e == expected,
              let rawPrice = kind == .last ? frame.c : frame.p,
              let price = Decimal(string: rawPrice, locale: Locale(identifier: "en_US_POSIX")),
              price > 0 else { throw MarketError.invalidQuote }
        return Quote(symbol: frame.s, price: price,
                     changePercent: kind == .last ? frame.P.flatMap { Decimal(string: $0, locale: Locale(identifier: "en_US_POSIX")) } : nil,
                     eventTime: Date(timeIntervalSince1970: frame.E / 1000))
    }

    private struct Frame: Decodable {
        let e: String
        let E: Double
        let s: String
        let c: String?
        let p: String?
        let P: String?
    }
    private struct Combined: Decodable { let data: Frame }
}

public enum MarketError: LocalizedError {
    case invalidQuote, http(Int), timeout
    public var errorDescription: String? {
        switch self {
        case .invalidQuote: return "行情数据格式不正确"
        case .http(let status): return "Binance 接口返回 HTTP \(status)"
        case .timeout: return "行情连接超时"
        }
    }
}

public enum BinanceAPI {
    public static func makeSession(proxyPort: Int? = nil) -> URLSession {
        let configuration = URLSessionConfiguration.default
        configuration.waitsForConnectivity = false
        configuration.timeoutIntervalForRequest = 20
        if let port = proxyPort, (1...65535).contains(port) {
            configuration.connectionProxyDictionary = [
                kCFNetworkProxiesHTTPEnable as String: 1,
                kCFNetworkProxiesHTTPProxy as String: "127.0.0.1",
                kCFNetworkProxiesHTTPPort as String: port,
                kCFNetworkProxiesHTTPSEnable as String: 1,
                kCFNetworkProxiesHTTPSProxy as String: "127.0.0.1",
                kCFNetworkProxiesHTTPSPort as String: port
            ]
        }
        return URLSession(configuration: configuration)
    }

    public static func streamURL(symbols: [String], kind: PriceKind) -> URL {
        let streams = Watchlist(symbols).symbols.map { "\($0.lowercased())@\(kind.stream)" }.joined(separator: "/")
        // Regular market streams require the /market route (Binance URL migration).
        return URL(string: "wss://fstream.binance.com/market/stream?streams=\(streams)")!
    }

    public static func contracts(session: URLSession = .shared) async throws -> [Contract] {
        var request = URLRequest(url: URL(string: "https://fapi.binance.com/fapi/v1/exchangeInfo")!)
        request.timeoutInterval = 20
        let (data, response) = try await session.data(for: request)
        let code = (response as? HTTPURLResponse)?.statusCode ?? 0
        guard code == 200 else { throw MarketError.http(code) }
        return try decodeContracts(data)
    }

    public static func decodeContracts(_ data: Data) throws -> [Contract] {
        struct Exchange: Decodable { let symbols: [Contract] }
        return try JSONDecoder().decode(Exchange.self, from: data).symbols
            .filter(\.isSupported).sorted { $0.symbol < $1.symbol }
    }

    public static func messageData(_ message: URLSessionWebSocketTask.Message) -> Data {
        switch message {
        case .data(let data): return data
        case .string(let string): return Data(string.utf8)
        @unknown default: return Data()
        }
    }
}

public enum PriceText {
    public static func format(_ price: Decimal, compact: Bool) -> String {
        let formatter = NumberFormatter()
        formatter.locale = Locale(identifier: "en_US")
        formatter.numberStyle = .decimal
        formatter.usesGroupingSeparator = true
        formatter.minimumFractionDigits = compact ? 0 : 2
        // Keep tiny contracts visible instead of rounding their price to zero.
        if price >= 1000 { formatter.maximumFractionDigits = compact ? 0 : 2 }
        else if price >= 1 { formatter.maximumFractionDigits = 2 }
        else if price >= Decimal(string: "0.01")! { formatter.maximumFractionDigits = 4 }
        else { formatter.maximumFractionDigits = 8 }
        return formatter.string(from: NSDecimalNumber(decimal: price)) ?? "—"
    }
}
