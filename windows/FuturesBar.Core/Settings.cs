using System.Text.Json;
using System.Text.Json.Serialization;

namespace FuturesBar.Core;

public sealed record AppSettings
{
    public List<string> Symbols { get; init; } = ["BTCUSDT", "ETHUSDT", "SOLUSDT"];
    public PriceKind PriceKind { get; init; } = PriceKind.Last;
    public bool ManualProxy { get; init; }
    public int? ProxyPort { get; init; }
    public bool Locked { get; init; }
    public bool Topmost { get; init; } = true;
    public bool Compact { get; init; } = true;
    public double Opacity { get; init; } = 0.95;
    public double? Left { get; init; }
    public double? Top { get; init; }

    public AppSettings Normalize() => this with
    {
        Symbols = new Watchlist(Symbols).Symbols.ToList(),
        PriceKind = Enum.IsDefined(PriceKind) ? PriceKind : PriceKind.Last,
        ManualProxy = ManualProxy && ProxyPort is >= 1 and <= 65535,
        ProxyPort = ProxyPort is >= 1 and <= 65535 ? ProxyPort : null,
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, 0.35, 1) : 0.95,
        Left = Left.HasValue && double.IsFinite(Left.Value) ? Left : null,
        Top = Top.HasValue && double.IsFinite(Top.Value) ? Top : null
    };

    public static int? ValidateProxy(bool enabled, string portText, int? previousPort)
    {
        if (!enabled) return previousPort;
        if (!int.TryParse(portText.Trim(), out var port) || port is < 1 or > 65535)
            throw new ArgumentException("代理端口需要填写 1–65535 之间的整数。");
        return port;
    }
}

public sealed class SettingsFile(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter() }
    };
    public string? Error { get; private set; }
    public AppSettings Load()
    {
        Error = null;
        try
        {
            return File.Exists(path)
                ? (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new()).Normalize()
                : new AppSettings();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            Error = "无法读取本地设置，已使用默认设置：" + error.Message;
            return new AppSettings();
        }
    }

    public bool Save(AppSettings settings)
    {
        Error = null;
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings.Normalize(), JsonOptions));
            File.Move(temporary, path, true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Error = "无法保存本地设置：" + error.Message;
            return false;
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }
}
