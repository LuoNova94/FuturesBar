using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FuturesBar.Core;

public enum PriceKind { Last, Mark }

public sealed record Contract(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("baseAsset")] string BaseAsset,
    [property: JsonPropertyName("quoteAsset")] string QuoteAsset,
    [property: JsonPropertyName("contractType")] string ContractType,
    [property: JsonPropertyName("status")] string Status)
{
    public bool Supported => QuoteAsset == "USDT" && (ContractType is "PERPETUAL" or "TRADIFI_PERPETUAL") && Status == "TRADING";
    public string DisplayName => BaseAsset == "SPCX" ? "SPCX (SpaceX)" : BaseAsset;
    public string MarketLabel => ContractType == "TRADIFI_PERPETUAL" ? "TradFi · USDT 永续" : "USDT 永续";
    public bool MatchesSearch(string query)
    {
        var term = query.Trim();
        var aliases = BaseAsset == "KORU" ? "韩国 ETF" : "";
        return term.Length == 0 || $"{Symbol} {DisplayName} {aliases}".Contains(term, StringComparison.OrdinalIgnoreCase);
    }
    public static string AssetName(string symbol) => symbol.EndsWith("USDT", StringComparison.Ordinal)
        ? symbol[..^4] : symbol;
}

public sealed partial class Watchlist
{
    public const int Limit = 3;
    private readonly List<string> symbols;
    public IReadOnlyList<string> Symbols => symbols.AsReadOnly();

    public Watchlist(IEnumerable<string>? values)
    {
        symbols = (values ?? []).Select(s => (s ?? "").ToUpperInvariant())
            .Where(s => SymbolPattern().IsMatch(s)).Distinct(StringComparer.Ordinal).Take(Limit).ToList();
        if (symbols.Count == 0) symbols.Add("BTCUSDT");
    }

    public bool Toggle(string symbol)
    {
        if (symbols.Contains(symbol)) return symbols.Count > 1 && symbols.Remove(symbol);
        if (symbols.Count >= Limit || !SymbolPattern().IsMatch(symbol)) return false;
        symbols.Add(symbol);
        return true;
    }

    public void Move(string symbol, int offset)
    {
        var index = symbols.IndexOf(symbol);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= symbols.Count) return;
        (symbols[index], symbols[target]) = (symbols[target], symbols[index]);
    }

    [GeneratedRegex("^[A-Z0-9]+USDT$", RegexOptions.CultureInvariant)]
    private static partial Regex SymbolPattern();
}

public sealed record Quote(string Symbol, decimal Price, decimal? ChangePercent, DateTimeOffset EventTime)
{
    public bool IsFresh(DateTimeOffset now) => (now - EventTime).TotalSeconds is >= -10 and < 15;

    public static Quote? Decode(ReadOnlyMemory<byte> data, PriceKind kind)
    {
        try
        {
            using var document = JsonDocument.Parse(data);
            var frame = document.RootElement;
            if (frame.TryGetProperty("data", out var combined)) frame = combined;
            var expected = kind == PriceKind.Last ? "24hrTicker" : "markPriceUpdate";
            if (frame.GetProperty("e").GetString() != expected) return null;
            var raw = frame.GetProperty(kind == PriceKind.Last ? "c" : "p").GetString();
            const NumberStyles style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;
            if (!decimal.TryParse(raw, style, CultureInfo.InvariantCulture, out var price) || price <= 0) return null;
            var symbol = frame.GetProperty("s").GetString();
            if (string.IsNullOrEmpty(symbol)) return null;
            decimal? change = null;
            if (kind == PriceKind.Last && frame.TryGetProperty("P", out var p)
                && decimal.TryParse(p.GetString(), style, CultureInfo.InvariantCulture, out var percent)) change = percent;
            return new Quote(symbol, price, change,
                DateTimeOffset.FromUnixTimeMilliseconds(frame.GetProperty("E").GetInt64()));
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException
                                      or ArgumentOutOfRangeException or FormatException or OverflowException)
        {
            return null;
        }
    }
}

public static class PriceText
{
    public static string Format(decimal price, bool compact = false)
    {
        var digits = price >= 1000 ? (compact ? 0 : 2) : price >= 1 ? 2 : price >= 0.01m ? 4 : 8;
        var minimum = compact ? 0 : Math.Min(2, digits);
        var format = "#,##0" + (digits > 0 ? "." + new string('0', minimum) + new string('#', digits - minimum) : "");
        return price.ToString(format, CultureInfo.InvariantCulture);
    }
}
