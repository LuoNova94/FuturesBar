using System.IO;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace FuturesBar.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\FuturesBar.Windows", out var first);
        if (!first)
        {
            MessageBox.Show("合约行情已经在运行，请从右下角系统托盘打开。", "合约行情");
            return;
        }
        new FuturesApplication().Run();
    }
}

internal sealed class FuturesApplication : Application
{
    private readonly SettingsFile file = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FuturesBar", "settings.json"));
    private readonly Dictionary<string, Quote> quotes = [];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly SemaphoreSlim feedGate = new(1, 1);
    private Forms.NotifyIcon? tray;
    private FloatingWindow? floating;
    private SettingsWindow? settingsWindow;
    private BinanceFeed? feed;
    private CancellationTokenSource? catalogCancellation;
    private bool exiting;
    public bool IsQuitting => exiting;
    public AppSettings Settings { get; private set; } = new();
    public List<Contract> Contracts { get; private set; } = [];
    public string Status { get; private set; } = "正在连接行情…";
    public string? CatalogError { get; private set; }
    public bool LoadingCatalog { get; private set; }
    public string? SettingsError => file.Error;
    public event Action? Updated;
    public event Action? CatalogUpdated;
    public Quote? GetQuote(string symbol) => quotes.GetValueOrDefault(symbol);
    public bool Live => Status == "实时连接" && Settings.Symbols.All(s => GetQuote(s)?.IsFresh(DateTimeOffset.UtcNow) == true);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Settings = file.Load();
        Ui.InstallStyles(Resources);
        floating = new FloatingWindow(this);
        MainWindow = floating;
        CreateTray();
        timer.Tick += (_, _) => Refresh();
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Save(); };
        timer.Start();
        floating.Show();
        SystemEvents.PowerModeChanged += PowerModeChanged;
        _ = RestartFeedAsync();
        _ = LoadContractsAsync();
        if (file.Error != null) MessageBox.Show(file.Error, "设置提示");
    }

    private void CreateTray()
    {
        tray = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Text = "合约行情 · FuturesBar",
            Visible = true
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示 / 隐藏行情条", null, (_, _) => ToggleStrip());
        menu.Items.Add("品种与设置…", null, (_, _) => OpenSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        var locked = new Forms.ToolStripMenuItem("锁定位置") { Checked = Settings.Locked, CheckOnClick = true };
        locked.Click += (_, _) => Apply(Settings with { Locked = locked.Checked }, false);
        var topmost = new Forms.ToolStripMenuItem("始终置顶") { Checked = Settings.Topmost, CheckOnClick = true };
        topmost.Click += (_, _) => Apply(Settings with { Topmost = topmost.Checked }, false);
        menu.Items.Add(locked);
        menu.Items.Add(topmost);
        menu.Opening += (_, _) => { locked.Checked = Settings.Locked; topmost.Checked = Settings.Topmost; };
        menu.Items.Add("重新连接行情", null, (_, _) => { _ = RestartFeedAsync(); });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, async (_, _) => await QuitAsync());
        tray.ContextMenuStrip = menu;
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ToggleStrip(); };
        tray.MouseDoubleClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) OpenSettings(); };
    }

    public void ToggleStrip()
    {
        if (floating == null) return;
        if (floating.IsVisible) floating.Hide();
        else { floating.Show(); floating.ClampToScreen(); }
    }

    public void HideStrip() => floating?.Hide();

    public void OpenSettings()
    {
        if (settingsWindow != null) { settingsWindow.Activate(); return; }
        settingsWindow = new SettingsWindow(this);
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
        settingsWindow.Activate();
        if (Contracts.Count == 0 && !LoadingCatalog) _ = LoadContractsAsync();
    }

    public void Apply(AppSettings next, bool restartMarket = true)
    {
        Settings = next.Normalize();
        Save();
        floating?.ApplyAppearance();
        Refresh();
        if (restartMarket)
        {
            _ = RestartFeedAsync();
            _ = LoadContractsAsync();
        }
    }

    public void RememberPosition(double left, double top)
    {
        Settings = Settings with { Left = left, Top = top };
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void Save()
    {
        file.Save(Settings);
        Updated?.Invoke();
    }

    private void Refresh()
    {
        floating?.RefreshQuotes();
        Updated?.Invoke();
        if (tray != null)
        {
            var text = string.Join(" · ", Settings.Symbols.Select(s =>
                $"{Contract.AssetName(s)} {(GetQuote(s) is { } q && q.IsFresh(DateTimeOffset.UtcNow) ? PriceText.Format(q.Price) : "—")}"));
            tray.Text = text.Length <= 127 ? text : text[..124] + "…";
        }
    }

    private async Task RestartFeedAsync()
    {
        await feedGate.WaitAsync();
        try
        {
            var old = feed;
            feed = null;
            if (old != null) await old.DisposeAsync();
            if (exiting) return;
            quotes.Clear();
            Status = "正在连接行情…";
            var next = new BinanceFeed(Settings);
            feed = next;
            next.QuoteReceived += quote => Dispatcher.BeginInvoke(() =>
            {
                if (!ReferenceEquals(feed, next) || exiting) return;
                if (quotes.TryGetValue(quote.Symbol, out var previous) && previous.EventTime > quote.EventTime) return;
                quotes[quote.Symbol] = quote;
            });
            next.StatusChanged += status => Dispatcher.BeginInvoke(() =>
            {
                if (!ReferenceEquals(feed, next) || exiting) return;
                Status = status;
                if (status.StartsWith("连接中断", StringComparison.Ordinal)) quotes.Clear();
            });
            _ = next.StartAsync();
            Refresh();
        }
        finally { feedGate.Release(); }
    }

    public async Task LoadContractsAsync()
    {
        catalogCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        var previous = catalogCancellation;
        catalogCancellation = cancellation;
        previous?.Dispose();
        LoadingCatalog = true;
        CatalogError = null;
        CatalogUpdated?.Invoke();
        try
        {
            var contracts = await BinanceApi.ContractsAsync(Settings, cancellation.Token);
            if (!ReferenceEquals(catalogCancellation, cancellation) || exiting) return;
            Contracts = contracts;
            if (contracts.Count == 0) CatalogError = "未获取到可用合约，请重试。";
        }
        catch (Exception error) when (error is System.Net.Http.HttpRequestException or OperationCanceledException
                                     or System.Text.Json.JsonException or System.IO.IOException)
        {
            if (!ReferenceEquals(catalogCancellation, cancellation) || exiting) return;
            CatalogError = "无法加载合约列表：" + error.Message;
        }
        finally
        {
            if (ReferenceEquals(catalogCancellation, cancellation))
            {
                LoadingCatalog = false;
                CatalogUpdated?.Invoke();
            }
        }
    }

    private void PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(async () =>
        {
            if (exiting) return;
            if (e.Mode == PowerModes.Suspend)
            {
                await feedGate.WaitAsync();
                try { if (feed != null) { var old = feed; feed = null; await old.DisposeAsync(); } }
                finally { feedGate.Release(); }
                quotes.Clear();
                Status = "电脑休眠中";
            }
            else if (e.Mode == PowerModes.Resume) await RestartFeedAsync();
        });
    }

    public async Task QuitAsync()
    {
        if (exiting) return;
        exiting = true;
        SystemEvents.PowerModeChanged -= PowerModeChanged;
        timer.Stop(); saveTimer.Stop();
        catalogCancellation?.Cancel();
        await feedGate.WaitAsync();
        try { if (feed != null) { await feed.DisposeAsync(); feed = null; } }
        finally { feedGate.Release(); }
        Save();
        settingsWindow?.Close();
        tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.PowerModeChanged -= PowerModeChanged;
        tray?.Dispose();
        base.OnExit(e);
    }
}
