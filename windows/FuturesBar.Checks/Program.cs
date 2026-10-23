using System.Text;
using FuturesBar.Core;

var tests = new (string Name, Action Run)[]
{
    ("watchlist cap, ordering and final symbol", () =>
    {
        var list = new Watchlist(["btcusdt", "ETHUSDT", "BTCUSDT", "SOLUSDT", "DOGEUSDT"]);
        Check(list.Symbols.SequenceEqual(["BTCUSDT", "ETHUSDT", "SOLUSDT"]));
        Check(!list.Toggle("DOGEUSDT")); Check(list.Toggle("ETHUSDT"));
        Check(list.Toggle("DOGEUSDT")); list.Move("DOGEUSDT", -1);
        Check(list.Symbols.SequenceEqual(["BTCUSDT", "DOGEUSDT", "SOLUSDT"]));
        var single = new Watchlist([]);
        Check(!single.Toggle("BTCUSDT")); Check(!single.Toggle("ethusdt@ticker/foo"));
        Check(!single.Toggle("BTCUSD"));
    }),
    ("latest price uses c, not absolute change p", () =>
    {
        var quote = Decode("""{"stream":"btcusdt@ticker","data":{"e":"24hrTicker","E":1720000000000,"s":"BTCUSDT","c":"65000.10","p":"123.4","P":"2.35"}}""", PriceKind.Last);
        Check(quote?.Price == 65000.10m && quote.ChangePercent == 2.35m);
        Check(quote!.EventTime.ToUnixTimeMilliseconds() == 1720000000000);
    }),
    ("mark price, stale prices and wrong mode", () =>
    {
        const string raw = """{"e":"markPriceUpdate","E":1720000000000,"s":"BTCUSDT","p":"65001.12345678","r":"0.0001"}""";
        var quote = Decode(raw, PriceKind.Mark)!;
        Check(quote.Price == 65001.12345678m && quote.ChangePercent == null);
        Check(quote.IsFresh(quote.EventTime.AddSeconds(14)));
        Check(!quote.IsFresh(quote.EventTime.AddSeconds(15)));
        Check(!quote.IsFresh(quote.EventTime.AddSeconds(-11)));
        Check(Decode(raw, PriceKind.Last) == null);
        foreach (var price in new[] { "0", "-1", "12abc", "NaN" })
            Check(Decode($$"""{"e":"24hrTicker","E":1720000000000,"s":"BTCUSDT","c":"{{price}}"}""", PriceKind.Last) == null);
        Check(Decode("{}", PriceKind.Last) == null);
    }),
    ("routed URL and symbol normalization", () =>
    {
        var settings = new AppSettings { Symbols = ["BTCUSDT", "ETHUSDT"], PriceKind = PriceKind.Mark };
        Check(BinanceApi.StreamUrl(settings).OriginalString == "wss://fstream.binance.com/market/stream?streams=btcusdt@markPrice@1s/ethusdt@markPrice@1s");
        Check(new Contract("ETHUSDC", "ETH", "USDC", "PERPETUAL", "TRADING").Supported == false);
        Check(new Contract("BTCUSDT", "BTC", "USDT", "CURRENT_QUARTER", "TRADING").Supported == false);
        Check(new Contract("OLDUSDT", "OLD", "USDT", "PERPETUAL", "SETTLING").Supported == false);
    }),
    ("TradFi contracts, aliases and mixed watchlist", () =>
    {
        var spacex = new Contract("SPCXUSDT", "SPCX", "USDT", "TRADIFI_PERPETUAL", "TRADING");
        var koru = new Contract("KORUUSDT", "KORU", "USDT", "TRADIFI_PERPETUAL", "TRADING");
        Check(spacex.Supported && koru.Supported);
        Check(spacex.MatchesSearch(" spaceX ") && spacex.MatchesSearch("spcx"));
        Check(!spacex.MatchesSearch("KORU") && koru.MatchesSearch("韩国"));
        Check(!(spacex with { QuoteAsset = "USD1" }).Supported);
        Check(!(koru with { Status = "SETTLING" }).Supported);
        Check(!(koru with { ContractType = "UNKNOWN" }).Supported);
        var list = new Watchlist(["BTCUSDT", "KORUUSDT", "SPCXUSDT"]);
        Check(!list.Toggle("TSLAUSDT") && list.Symbols.Count == 3);
        Check(BinanceApi.StreamUrl(new AppSettings { Symbols = list.Symbols.ToList() }).OriginalString.Contains("spcxusdt@ticker"));
    }),
    ("proxy defaults, validation and system mode", () =>
    {
        var settings = new AppSettings();
        Check(!settings.ManualProxy && settings.ProxyPort == null);
        Check(ReferenceEquals(BinanceApi.Proxy(settings), System.Net.Http.HttpClient.DefaultProxy));
        Check(AppSettings.ValidateProxy(false, "", null) == null);
        Check(AppSettings.ValidateProxy(false, "bad draft", 1087) == 1087);
        Check(AppSettings.ValidateProxy(true, " 7890 ", null) == 7890);
        foreach (var value in new[] { "", "0", "65536", "abc" })
        {
            try { AppSettings.ValidateProxy(true, value, null); throw new Exception("accepted invalid port"); }
            catch (ArgumentException) { }
        }
    }),
    ("tiny prices and invariant formatting", () =>
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new("de-DE");
            Check(PriceText.Format(0.00001234m, true) == "0.00001234");
            Check(PriceText.Format(65000.12m) == "65,000.12");
            Check(PriceText.Format(65000.12m, true) == "65,000");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
    }),
    ("settings persistence and corrupt-file recovery", () =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "futuresbar-check-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var file = new SettingsFile(path);
            Check(!file.Load().ManualProxy);
            Check(file.Save(new AppSettings { Symbols = ["BTCUSDT", "SOLUSDT"], Locked = true,
                Opacity = 0.7, ManualProxy = true, ProxyPort = 1087, Left = 100, Top = 200 }));
            var loaded = file.Load();
            Check(loaded.Locked && loaded.Opacity == 0.7 && loaded.ProxyPort == 1087 && loaded.Left == 100);
            Check(loaded.Symbols.SequenceEqual(["BTCUSDT", "SOLUSDT"]));
            File.WriteAllText(path, "invalid JSON");
            Check(file.Load().Symbols.Count == 3 && file.Error != null);
            Check(new AppSettings { Opacity = double.NaN, Left = double.PositiveInfinity, ManualProxy = true, ProxyPort = 0 }
                .Normalize() is { Opacity: 0.95, Left: null, ManualProxy: false });
            Check(!Directory.EnumerateFiles(directory, "*.tmp").Any());
        }
        finally { Directory.Delete(directory, true); }
    })
};

foreach (var (name, run) in tests) { run(); Console.WriteLine("PASS " + name); }
Console.WriteLine($"{tests.Length} checks passed.");

if (args.Contains("--live"))
{
    var proxyPort = int.TryParse(Environment.GetEnvironmentVariable("FUTURES_PROXY_PORT"), out var port) ? (int?)port : null;
    var settings = new AppSettings { ManualProxy = proxyPort.HasValue, ProxyPort = proxyPort };
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
    var contracts = await BinanceApi.ContractsAsync(settings, timeout.Token);
    var symbols = Environment.GetEnvironmentVariable("FUTURES_TEST_SYMBOLS")?.Split(',').Select(s => s.ToUpperInvariant()).ToList()
        ?? new List<string> { "BTCUSDT", "ETHUSDT", "SOLUSDT" };
    Check(symbols.Count is >= 1 and <= 3 && symbols.All(s => contracts.Any(c => c.Symbol == s)) && contracts.All(c => c.Supported));
    settings = settings with { Symbols = symbols };
    Console.WriteLine($"REST OK: {contracts.Count} USDT perpetual contracts");
    foreach (var kind in Enum.GetValues<PriceKind>())
    {
        var ready = new TaskCompletionSource<Dictionary<string, Quote>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new Dictionary<string, Quote>();
        await using var feed = new BinanceFeed(settings with { PriceKind = kind });
        feed.QuoteReceived += quote =>
        {
            if (!quote.IsFresh(DateTimeOffset.UtcNow)) return;
            received[quote.Symbol] = quote;
            if (received.Count == symbols.Count) ready.TrySetResult(new(received));
        };
        _ = feed.StartAsync();
        var quotes = await ready.Task.WaitAsync(TimeSpan.FromSeconds(25));
        Console.WriteLine($"WebSocket {kind} OK: " + string.Join(", ", quotes.OrderBy(q => q.Key).Select(q => q.Key + "=" + PriceText.Format(q.Value.Price))));
    }
}

static Quote? Decode(string value, PriceKind kind) => Quote.Decode(Encoding.UTF8.GetBytes(value), kind);
static void Check(bool condition) { if (!condition) throw new InvalidOperationException("Check failed"); }
