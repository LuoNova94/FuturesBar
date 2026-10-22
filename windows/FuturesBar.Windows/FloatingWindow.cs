using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace FuturesBar.Windows;

internal sealed class FloatingWindow : Window
{
    private readonly FuturesApplication app;
    private readonly StackPanel chips = new() { Orientation = Orientation.Horizontal };
    private readonly Dictionary<string, (TextBlock price, TextBlock change)> labels = [];
    private readonly TextBlock dot = Ui.Text("●", 10, Ui.Accent);
    private readonly TextBlock status = Ui.Text("", 10, Ui.Muted);
    private string renderedSymbols = "";
    private bool ready;
    private bool clamping;

    public FloatingWindow(FuturesApplication app)
    {
        this.app = app;
        Title = "合约行情 · FuturesBar";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var handle = new StackPanel { Margin = new Thickness(3, 0, 12, 0) };
        handle.Children.Add(Ui.Text("⠿", 16, Ui.Muted));
        handle.Children.Add(dot);
        row.Children.Add(handle);
        row.Children.Add(chips);
        row.Children.Add(Ui.Button("⚙", app.OpenSettings));
        row.Children.Add(Ui.Button("×", app.HideStrip));
        var panel = new StackPanel();
        panel.Children.Add(row);
        status.Margin = new Thickness(24, 5, 0, 0);
        panel.Children.Add(status);
        var border = new Border
        {
            Background = Ui.Background, BorderBrush = Ui.Brush("#343B47"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11), Padding = new Thickness(12, 9, 10, 8), Child = panel
        };
        Content = border;
        border.MouseLeftButtonDown += (_, e) =>
        {
            if (InsideButton(e.OriginalSource as DependencyObject)) return;
            if (e.ClickCount == 2) { app.OpenSettings(); return; }
            if (app.Settings.Locked) return;
            try { DragMove(); ClampToScreen(); }
            catch (InvalidOperationException) { }
        };
        border.MouseRightButtonUp += (_, _) => app.OpenSettings();
        Loaded += (_, _) =>
        {
            if (app.Settings.Left is { } left && app.Settings.Top is { } top) { Left = left; Top = top; }
            else { Left = SystemParameters.WorkArea.Right - ActualWidth - 24; Top = SystemParameters.WorkArea.Top + 24; }
            ready = true;
            ClampToScreen();
        };
        SourceInitialized += (_, _) =>
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(WindowMessages);
        };
        LocationChanged += (_, _) => { if (ready && !clamping) app.RememberPosition(Left, Top); };
        SizeChanged += (_, _) => { if (ready) ClampToScreen(); };
        Closing += OnClosing;
        ApplyAppearance();
        RefreshQuotes();
    }

    public void ApplyAppearance()
    {
        Topmost = app.Settings.Topmost;
        Opacity = app.Settings.Opacity;
        Cursor = app.Settings.Locked ? Cursors.Arrow : Cursors.SizeAll;
    }

    public void RefreshQuotes()
    {
        var symbols = string.Join('|', app.Settings.Symbols);
        if (renderedSymbols != symbols)
        {
            renderedSymbols = symbols; chips.Children.Clear(); labels.Clear();
            foreach (var symbol in app.Settings.Symbols)
            {
                var column = new StackPanel { Margin = new Thickness(0, 0, 16, 0), MinWidth = 88 };
                var title = new StackPanel { Orientation = Orientation.Horizontal };
                title.Children.Add(Ui.Text(Contract.AssetName(symbol), 10, Ui.Muted));
                var change = Ui.Text("", 9, Ui.Muted); change.Margin = new Thickness(6, 0, 0, 0);
                title.Children.Add(change); column.Children.Add(title);
                var price = Ui.Text("—", 17); price.FontWeight = FontWeights.SemiBold;
                column.Children.Add(price);
                chips.Children.Add(column); labels[symbol] = (price, change);
            }
        }
        var now = DateTimeOffset.UtcNow;
        foreach (var (symbol, label) in labels)
        {
            var quote = app.GetQuote(symbol);
            var fresh = quote?.IsFresh(now) == true;
            var price = fresh ? PriceText.Format(quote!.Price, app.Settings.Compact) : "—";
            if (label.price.Text != price) label.price.Text = price;
            label.price.Foreground = fresh ? Brushes.White : Ui.Muted;
            var change = fresh && quote!.ChangePercent is { } p ? p.ToString("+0.00;-0.00;0.00", System.Globalization.CultureInfo.InvariantCulture) + "%" : "";
            if (label.change.Text != change) label.change.Text = change;
            label.change.Foreground = quote?.ChangePercent >= 0 ? Ui.Positive : Ui.Negative;
        }
        dot.Foreground = app.Live ? Ui.Positive : Ui.Accent;
        var message = app.Live
            ? $"BINANCE · USDT 永续 · {(app.Settings.PriceKind == PriceKind.Last ? "最新成交价" : "标记价格")}" : app.Status;
        if (status.Text != message) status.Text = message;
        ToolTip = app.Live ? "拖动调整位置 · 双击或右键打开设置" : app.Status + "；过期价格已隐藏";
    }

    private static bool InsideButton(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is Button) return true;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : null;
        }
        return false;
    }

    private IntPtr WindowMessages(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0021) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
        if (message is 0x007E or 0x02E0 or 0x001A) // display, DPI, or work-area change
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, ClampToScreen);
        return IntPtr.Zero;
    }

    public void ClampToScreen()
    {
        if (!ready || clamping) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!GetWindowRect(hwnd, out var rectangle)) return;
        var monitor = MonitorFromWindow(hwnd, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return;
        var x = Math.Clamp(rectangle.Left, info.Work.Left, Math.Max(info.Work.Left, info.Work.Right - (rectangle.Right - rectangle.Left)));
        var y = Math.Clamp(rectangle.Top, info.Work.Top, Math.Max(info.Work.Top, info.Work.Bottom - (rectangle.Bottom - rectangle.Top)));
        clamping = true;
        try { SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010); }
        finally { clamping = false; }
        app.RememberPosition(Left, Top);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (Application.Current.ShutdownMode == ShutdownMode.OnExplicitShutdown && !Dispatcher.HasShutdownStarted)
        {
            // Application shutdown is initiated by the tray's explicit Exit action.
            if (app.IsQuitting) return;
            e.Cancel = true; Hide();
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
