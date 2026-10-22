using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;

namespace FuturesBar.Core;

public static class BinanceApi
{
    public static IWebProxy Proxy(AppSettings settings) => settings.ManualProxy && settings.ProxyPort is >= 1 and <= 65535
        ? new WebProxy(new Uri($"http://127.0.0.1:{settings.ProxyPort}"))
        : HttpClient.DefaultProxy;

    public static Uri StreamUrl(AppSettings settings)
    {
        var stream = settings.PriceKind == PriceKind.Last ? "ticker" : "markPrice@1s";
        var symbols = new Watchlist(settings.Symbols).Symbols;
        return new Uri("wss://fstream.binance.com/market/stream?streams="
            + string.Join('/', symbols.Select(s => s.ToLowerInvariant() + "@" + stream)));
    }

    public static async Task<List<Contract>> ContractsAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        using var handler = new HttpClientHandler { Proxy = Proxy(settings), UseProxy = true };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        var result = await client.GetFromJsonAsync<ExchangeInfo>("https://fapi.binance.com/fapi/v1/exchangeInfo", cancellationToken)
            .ConfigureAwait(false);
        return (result?.Symbols ?? []).Where(c => c.Supported).OrderBy(c => c.Symbol, StringComparer.Ordinal).ToList();
    }

    private sealed record ExchangeInfo([property: System.Text.Json.Serialization.JsonPropertyName("symbols")] List<Contract> Symbols);
}

// A feed belongs to one immutable settings snapshot. Replacing it cancels the
// old socket and retry delay before the new subscription becomes current.
public sealed class BinanceFeed : IAsyncDisposable
{
    private readonly AppSettings settings;
    private readonly CancellationTokenSource stop = new();
    private ClientWebSocket? socket;
    private Task? running;
    public event Action<Quote>? QuoteReceived;
    public event Action<string>? StatusChanged;

    public BinanceFeed(AppSettings settings) => this.settings = settings.Normalize();
    public Task StartAsync() => running ??= RunAsync(stop.Token);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var failures = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            using var current = new ClientWebSocket();
            socket = current;
            current.Options.Proxy = BinanceApi.Proxy(settings);
            current.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            current.Options.KeepAliveTimeout = TimeSpan.FromSeconds(15);
            try
            {
                StatusChanged?.Invoke("正在连接行情…");
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(20));
                    await current.ConnectAsync(BinanceApi.StreamUrl(settings), timeout.Token).ConfigureAwait(false);
                }
                var buffer = new byte[8192];
                while (!cancellationToken.IsCancellationRequested)
                {
                    using var message = new MemoryStream();
                    ValueWebSocketReceiveResult part;
                    do
                    {
                        part = await current.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                        if (part.MessageType == WebSocketMessageType.Close) throw new WebSocketException("行情服务器已关闭连接");
                        if (message.Length + part.Count > 262144) throw new WebSocketException("行情消息超出大小限制");
                        message.Write(buffer, 0, part.Count);
                    } while (!part.EndOfMessage);
                    if (part.MessageType != WebSocketMessageType.Text) continue;
                    var quote = Quote.Decode(message.ToArray(), settings.PriceKind);
                    if (quote == null || !settings.Symbols.Contains(quote.Symbol)) continue;
                    failures = 0;
                    QuoteReceived?.Invoke(quote);
                    StatusChanged?.Invoke("实时连接");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception error) when (error is WebSocketException or HttpRequestException or OperationCanceledException or IOException)
            {
                if (cancellationToken.IsCancellationRequested) break;
                current.Abort();
                var delay = TimeSpan.FromSeconds(Math.Min(30, 1 << Math.Min(++failures, 5)));
                StatusChanged?.Invoke($"连接中断，{delay.TotalSeconds:0} 秒后重试");
                try { await Task.Delay(delay, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
            finally { if (ReferenceEquals(socket, current)) socket = null; }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync().ConfigureAwait(false);
        try { socket?.Abort(); } catch (ObjectDisposedException) { }
        if (running != null) await running.ConfigureAwait(false);
        stop.Dispose();
    }
}
