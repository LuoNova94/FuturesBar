using System.Globalization;
using System.IO;
using Microsoft.Win32;

namespace FuturesBar.Windows;

internal sealed class SettingsWindow : Window
{
    private readonly FuturesApplication app;
    private readonly Watchlist watchlist;
    private readonly StackPanel selectedPanel = new();
    private readonly StackPanel resultsPanel = new();
    private readonly Dictionary<string, TextBlock> prices = [];
    private readonly TextBox search = new();
    private readonly TextBlock count = Ui.Text("", 11, Ui.Muted);
    private readonly TextBlock error = Ui.Text("", 11, Ui.Negative);
    private readonly TextBlock status = Ui.Text("", 10, Ui.Muted);
    private readonly RadioButton last = new() { Content = "最新成交价", Foreground = Brushes.White, GroupName = "PriceKind", Margin = new Thickness(0, 0, 24, 0) };
    private readonly RadioButton mark = new() { Content = "标记价格", Foreground = Brushes.White, GroupName = "PriceKind" };
    private readonly CheckBox proxy;
    private readonly TextBox port;
    private readonly CheckBox locked;
    private readonly CheckBox topmost;
    private readonly CheckBox compact;
    private readonly CheckBox startup;
    private readonly Slider opacity;
    private readonly StackPanel proxyRow;
    private bool rebuilding;
    private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public SettingsWindow(FuturesApplication app)
    {
        this.app = app;
        watchlist = new Watchlist(app.Settings.Symbols);
        Title = "合约行情 · 品种与设置";
        Width = 560; MinWidth = 500; Height = Math.Min(860, SystemParameters.WorkArea.Height - 48);
        MinHeight = 450;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Ui.Background;
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI");
        var layout = new DockPanel { Margin = new Thickness(24) };
        var footer = new StackPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        footer.Children.Add(new Border { Height = 1, Background = Ui.Card, Margin = new Thickness(0, 12, 0, 8) });
        error.TextWrapping = TextWrapping.Wrap;
        footer.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(Ui.Button("取消", Close));
        buttons.Children.Add(Ui.Button("保存并应用", Apply, true));
        footer.Children.Add(buttons); layout.Children.Add(footer);

        var body = new StackPanel();
        var title = Ui.Text("合约行情", 22); title.FontWeight = FontWeights.SemiBold;
        body.Children.Add(title);
        var subtitle = Ui.Text("BINANCE · USDT 永续 · 最多 3 个品种", 11, Ui.Muted);
        subtitle.Margin = new Thickness(0, 5, 0, 16); body.Children.Add(subtitle);
        body.Children.Add(selectedPanel);
        body.Children.Add(count);
        search.Margin = new Thickness(0, 10, 0, 8);
        search.ToolTip = "搜索合约，如 BTC、DOGE";
        System.Windows.Automation.AutomationProperties.SetName(search, "搜索合约品种");
        search.TextChanged += (_, _) => RebuildResults();
        body.Children.Add(search);
        var catalog = new ScrollViewer { Content = resultsPanel, Height = 170, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        body.Children.Add(catalog);
        var reload = Ui.Button("重新加载合约列表", () => { _ = app.LoadContractsAsync(); });
        reload.HorizontalAlignment = HorizontalAlignment.Left; body.Children.Add(reload);

        Section(body, "价格类型");
        var kinds = new StackPanel { Orientation = Orientation.Horizontal };
        last.IsChecked = app.Settings.PriceKind == PriceKind.Last;
        mark.IsChecked = app.Settings.PriceKind == PriceKind.Mark;
        kinds.Children.Add(last); kinds.Children.Add(mark); body.Children.Add(kinds);

        Section(body, "悬浮条");
        locked = Ui.Check("锁定位置", app.Settings.Locked);
        topmost = Ui.Check("始终置顶", app.Settings.Topmost);
        compact = Ui.Check("紧凑价格（千元以上取整）", app.Settings.Compact);
        startup = Ui.Check("登录 Windows 后启动", IsStartupEnabled());
        body.Children.Add(locked); body.Children.Add(topmost); body.Children.Add(compact); body.Children.Add(startup);
        var opacityRow = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var opacityTitle = Ui.Text("不透明度", 11, Ui.Muted); opacityTitle.Margin = new Thickness(0, 0, 20, 0);
        opacityRow.Children.Add(opacityTitle);
        opacity = new Slider { Minimum = 0.35, Maximum = 1, Value = app.Settings.Opacity, TickFrequency = 0.05, IsSnapToTickEnabled = true };
        opacityRow.Children.Add(opacity); body.Children.Add(opacityRow);

        Section(body, "网络");
        var networkHint = Ui.Text("默认跟随系统网络。手动代理只影响本工具，保存后生效。", 10, Ui.Muted);
        networkHint.TextWrapping = TextWrapping.Wrap; body.Children.Add(networkHint);
        proxy = Ui.Check("手动配置本机 HTTP / 混合代理", app.Settings.ManualProxy);
        body.Children.Add(proxy);
        proxyRow = new StackPanel { Orientation = Orientation.Horizontal };
        proxyRow.Children.Add(Ui.Text("127.0.0.1 : ", 11, Ui.Muted));
        port = new TextBox { Text = app.Settings.ProxyPort?.ToString(CultureInfo.InvariantCulture) ?? "", Width = 90 };
        System.Windows.Automation.AutomationProperties.SetName(port, "本机代理端口");
        proxyRow.Children.Add(port); body.Children.Add(proxyRow);
        proxyRow.Visibility = proxy.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        proxy.Checked += (_, _) => proxyRow.Visibility = Visibility.Visible;
        proxy.Unchecked += (_, _) => proxyRow.Visibility = Visibility.Collapsed;
        status.Margin = new Thickness(0, 14, 0, 0); status.TextWrapping = TextWrapping.Wrap;
        body.Children.Add(status);
        layout.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = layout;
        app.Updated += RefreshStatus;
        app.CatalogUpdated += RebuildResults;
        Closed += (_, _) => { app.Updated -= RefreshStatus; app.CatalogUpdated -= RebuildResults; };
        RebuildSelected(); RebuildResults(); RefreshStatus();
    }

    private static void Section(Panel panel, string title)
    {
        panel.Children.Add(new Border { Background = Ui.Card, Height = 1, Margin = new Thickness(0, 16, 0, 12) });
        var text = Ui.Text(title, 12); text.FontWeight = FontWeights.SemiBold;
        text.Margin = new Thickness(0, 0, 0, 8); panel.Children.Add(text);
    }

    private void RebuildSelected()
    {
        selectedPanel.Children.Clear(); prices.Clear();
        foreach (var symbol in watchlist.Symbols)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var controls = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(controls, Dock.Right);
            var up = Ui.Button("↑", () => { watchlist.Move(symbol, -1); SelectionChanged(); });
            up.IsEnabled = watchlist.Symbols[0] != symbol;
            var down = Ui.Button("↓", () => { watchlist.Move(symbol, 1); SelectionChanged(); });
            down.IsEnabled = watchlist.Symbols[^1] != symbol;
            var remove = Ui.Button("−", () => { watchlist.Toggle(symbol); SelectionChanged(); });
            remove.IsEnabled = watchlist.Symbols.Count > 1;
            controls.Children.Add(up); controls.Children.Add(down); controls.Children.Add(remove);
            row.Children.Add(controls);
            var text = new StackPanel();
            text.Children.Add(Ui.Text(Contract.AssetName(symbol) + " / USDT", 12));
            var price = Ui.Text("—", 16); text.Children.Add(price); prices[symbol] = price;
            row.Children.Add(text);
            selectedPanel.Children.Add(new Border { Background = Ui.Card, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 8, 8), Child = row, Margin = new Thickness(0, 0, 0, 6) });
        }
        count.Text = $"已选 {watchlist.Symbols.Count} / 3" + (watchlist.Symbols.Count == 3 ? " · 添加前请先移除一个品种" : " · 保存后开始接收新行情");
        RefreshStatus();
    }

    private void SelectionChanged() { RebuildSelected(); RebuildResults(); }

    private void RebuildResults()
    {
        if (rebuilding) return;
        rebuilding = true;
        try
        {
            resultsPanel.Children.Clear();
            if (app.LoadingCatalog) { resultsPanel.Children.Add(Ui.Text("正在加载合约列表…", 11, Ui.Muted)); return; }
            if (app.CatalogError is { } message)
            {
                var text = Ui.Text(message, 11, Ui.Negative); text.TextWrapping = TextWrapping.Wrap;
                resultsPanel.Children.Add(text); return;
            }
            var term = search.Text.Trim();
            string[] favorites = ["BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "DOGEUSDT", "XRPUSDT"];
            var matches = app.Contracts.Where(c => c.Symbol.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => Array.IndexOf(favorites, c.Symbol) is var i && i >= 0 ? i : 999)
                .ThenBy(c => c.Symbol, StringComparer.Ordinal).Take(100).ToList();
            foreach (var contract in matches)
            {
                var selected = watchlist.Symbols.Contains(contract.Symbol);
                var checkbox = Ui.Check(contract.BaseAsset + "  / USDT 永续", selected);
                checkbox.IsEnabled = selected ? watchlist.Symbols.Count > 1 : watchlist.Symbols.Count < 3;
                checkbox.Click += (_, _) =>
                {
                    if (rebuilding) return;
                    watchlist.Toggle(contract.Symbol);
                    SelectionChanged();
                };
                resultsPanel.Children.Add(checkbox);
            }
            if (matches.Count == 0) resultsPanel.Children.Add(Ui.Text("没有匹配的可交易 USDT 永续合约", 11, Ui.Muted));
            if (matches.Count == 100) resultsPanel.Children.Add(Ui.Text("输入名称可搜索更多合约", 10, Ui.Muted));
        }
        finally { rebuilding = false; }
    }

    private void RefreshStatus()
    {
        status.Text = app.SettingsError ?? app.Status;
        foreach (var (symbol, label) in prices)
        {
            var quote = app.GetQuote(symbol);
            label.Text = quote?.IsFresh(DateTimeOffset.UtcNow) == true ? PriceText.Format(quote.Price) : "—";
        }
    }

    private void Apply()
    {
        try
        {
            var proxyPort = AppSettings.ValidateProxy(proxy.IsChecked == true, port.Text, app.Settings.ProxyPort);
            SetStartup(startup.IsChecked == true);
            var next = app.Settings with
            {
                Symbols = watchlist.Symbols.ToList(), PriceKind = mark.IsChecked == true ? PriceKind.Mark : PriceKind.Last,
                Locked = locked.IsChecked == true, Topmost = topmost.IsChecked == true, Compact = compact.IsChecked == true,
                Opacity = opacity.Value, ManualProxy = proxy.IsChecked == true, ProxyPort = proxyPort
            };
            var marketChanged = !next.Symbols.SequenceEqual(app.Settings.Symbols) || next.PriceKind != app.Settings.PriceKind
                || next.ManualProxy != app.Settings.ManualProxy || next.ProxyPort != app.Settings.ProxyPort;
            app.Apply(next, marketChanged);
            if (app.SettingsError != null) { error.Text = app.SettingsError; return; }
            Close();
        }
        catch (Exception exception) when (exception is ArgumentException or UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            error.Text = exception.Message;
        }
    }

    private static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKey);
            return key?.GetValue("FuturesBar") as string == $"\"{Environment.ProcessPath}\"";
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException) { return false; }
    }

    private static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(StartupKey, true);
        if (enabled)
        {
            var executable = Environment.ProcessPath ?? throw new IOException("无法确认应用路径。");
            if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                throw new IOException("请使用发布后的 FuturesBar.exe 设置开机启动。");
            key.SetValue("FuturesBar", $"\"{executable}\"");
        }
        else key.DeleteValue("FuturesBar", false);
    }
}
