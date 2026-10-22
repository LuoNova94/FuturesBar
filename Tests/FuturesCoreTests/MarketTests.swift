import XCTest
@testable import FuturesCore

final class MarketTests: XCTestCase {
    func testWatchlistNeverExceedsThreeAndPreservesOrder() {
        var list = Watchlist(["btcusdt", "ETHUSDT", "BTCUSDT", "SOLUSDT", "BNBUSDT"])
        XCTAssertEqual(list.symbols, ["BTCUSDT", "ETHUSDT", "SOLUSDT"])
        XCTAssertFalse(list.toggle("BNBUSDT"))
        XCTAssertTrue(list.toggle("ETHUSDT"))
        XCTAssertTrue(list.toggle("DOGEUSDT"))
        list.move("DOGEUSDT", by: -1)
        XCTAssertEqual(list.symbols, ["BTCUSDT", "DOGEUSDT", "SOLUSDT"])
        list.move("BTCUSDT", by: -1)
        XCTAssertEqual(list.symbols.first, "BTCUSDT")
    }

    func testCannotRemoveFinalSymbolOrInjectStreamPath() {
        var list = Watchlist([])
        XCTAssertFalse(list.toggle("BTCUSDT"))
        XCTAssertFalse(list.toggle("ETHUSDT@ticker/foo"))
        XCTAssertEqual(list.symbols, ["BTCUSDT"])
    }

    func testLatestPriceDoesNotUseAbsolutePriceChange() throws {
        let quote = try Quote.decode(Data(#"{"stream":"btcusdt@ticker","data":{"e":"24hrTicker","E":1720000000000,"s":"BTCUSDT","c":"65000.10","p":"123.4","P":"2.35"}}"#.utf8), kind: .last)
        XCTAssertEqual(quote.price, Decimal(string: "65000.10"))
        XCTAssertEqual(quote.changePercent, Decimal(string: "2.35"))
        XCTAssertEqual(quote.eventTime.timeIntervalSince1970, 1720000000)
    }

    func testMarkPriceAndFreshness() throws {
        let quote = try Quote.decode(Data(#"{"e":"markPriceUpdate","E":1720000000000,"s":"BTCUSDT","p":"65001.12345678","r":"0.0001"}"#.utf8), kind: .mark)
        XCTAssertEqual(quote.price, Decimal(string: "65001.12345678"))
        XCTAssertNil(quote.changePercent)
        XCTAssertTrue(quote.isFresh(at: quote.eventTime.addingTimeInterval(14)))
        XCTAssertFalse(quote.isFresh(at: quote.eventTime.addingTimeInterval(15)))
        XCTAssertFalse(quote.isFresh(at: quote.eventTime.addingTimeInterval(-11)))
    }

    func testRejectsWrongPriceModeAndMalformedPrices() {
        for frame in [
            #"{"e":"markPriceUpdate","E":1720000000000,"s":"BTCUSDT","p":"65000"}"#,
            #"{"e":"24hrTicker","E":1720000000000,"s":"BTCUSDT","c":"0"}"#,
            #"{"e":"24hrTicker","E":1720000000000,"s":"BTCUSDT","c":"oops"}"#,
            #"{"result":null,"id":1}"#
        ] { XCTAssertThrowsError(try Quote.decode(Data(frame.utf8), kind: .last)) }
    }

    func testCatalogExcludesSpotDeliveryAndNonUSDTContracts() throws {
        let data = Data(#"{"symbols":[{"symbol":"BTCUSDT","baseAsset":"BTC","quoteAsset":"USDT","contractType":"PERPETUAL","status":"TRADING"},{"symbol":"ETHUSDC","baseAsset":"ETH","quoteAsset":"USDC","contractType":"PERPETUAL","status":"TRADING"},{"symbol":"BTCUSDT_261225","baseAsset":"BTC","quoteAsset":"USDT","contractType":"CURRENT_QUARTER","status":"TRADING"},{"symbol":"OLDUSDT","baseAsset":"OLD","quoteAsset":"USDT","contractType":"PERPETUAL","status":"SETTLING"}]}"#.utf8)
        XCTAssertEqual(try BinanceAPI.decodeContracts(data).map(\.symbol), ["BTCUSDT"])
    }

    func testRoutedCombinedStreamAndMarkInterval() {
        XCTAssertEqual(BinanceAPI.streamURL(symbols: ["BTCUSDT", "ETHUSDT"], kind: .last).absoluteString,
                       "wss://fstream.binance.com/market/stream?streams=btcusdt@ticker/ethusdt@ticker")
        XCTAssertTrue(BinanceAPI.streamURL(symbols: ["BTCUSDT"], kind: .mark).absoluteString.contains("/market/stream?streams=btcusdt@markPrice@1s"))
    }

    func testSmallContractPricesAreNotRoundedToZero() {
        XCTAssertEqual(PriceText.format(Decimal(string: "0.00001234")!, compact: true), "0.00001234")
        XCTAssertEqual(PriceText.format(Decimal(string: "65000.12")!, compact: false), "65,000.12")
        XCTAssertEqual(PriceText.format(Decimal(string: "65000.12")!, compact: true), "65,000")
    }
}
