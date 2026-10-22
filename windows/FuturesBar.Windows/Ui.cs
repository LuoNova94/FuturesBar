using System.Windows.Data;

namespace FuturesBar.Windows;

internal static class Ui
{
    public static readonly Brush Background = Brush("#13161D");
    public static readonly Brush Card = Brush("#20252F");
    public static readonly Brush Muted = Brush("#939DAC");
    public static readonly Brush Accent = Brush("#F1C449");
    public static readonly Brush Positive = Brush("#54C48D");
    public static readonly Brush Negative = Brush("#EC777A");
    public static Brush Brush(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    public static TextBlock Text(string text, double size = 12, Brush? color = null) => new()
    {
        Text = text, FontSize = size, Foreground = color ?? Brushes.White,
        FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"), VerticalAlignment = VerticalAlignment.Center
    };
    public static Button Button(string title, Action action, bool accent = false)
    {
        var button = new Button { Content = title, Background = accent ? Accent : Card,
            Foreground = accent ? Brushes.Black : Brushes.White, Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(3), Cursor = System.Windows.Input.Cursors.Hand };
        button.Click += (_, _) => action();
        return button;
    }
    public static CheckBox Check(string text, bool value) => new()
    {
        Content = text, IsChecked = value, Foreground = Brushes.White,
        VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 0, 6)
    };
    public static void InstallStyles(ResourceDictionary resources)
    {
        var box = new Style(typeof(TextBox));
        box.Setters.Add(new Setter(Control.BackgroundProperty, Card));
        box.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        box.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#384050")));
        box.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8)));
        box.Setters.Add(new Setter(TextBox.CaretBrushProperty, Brushes.White));
        resources[typeof(TextBox)] = box;
        var style = new Style(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
        border.SetBinding(Border.PaddingProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Button)) { VisualTree = border }));
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.8));
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.35));
        style.Triggers.Add(hover); style.Triggers.Add(disabled);
        resources[typeof(Button)] = style;
    }
}
