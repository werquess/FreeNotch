using System.Reflection;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace FreeNotch;

public sealed class SettingsWindow : Window
{
    private readonly Slider delay = new();

    public SettingsWindow(Settings settings, IslandWindow island)
    {
        Title = Strings.L("FreeNotch Settings");
        SystemBackdrop = new MicaBackdrop();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "FreeNotch.ico"));
        var scale = Native.GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(500 * scale), (int)(720 * scale)));

        var stack = new StackPanel { Spacing = 8, Padding = new Thickness(28, 20, 28, 28), MaxWidth = 640 };
        stack.Children.Add(new TextBlock { Text = "FreeNotch", FontSize = 28, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });

        stack.Children.Add(Header(Strings.L("Opening")));
        var open = new RadioButtons { Header = Strings.L("Open the island") };
        open.Items.Add(Strings.L("When I hover over the drop"));
        open.Items.Add(Strings.L("When I click the drop"));
        open.SelectedIndex = settings.OpenMode == OpenMode.Hover ? 0 : 1;
        open.SelectionChanged += (_, _) =>
        {
            var mode = open.SelectedIndex == 0 ? OpenMode.Hover : OpenMode.Click;
            island.SetOpenMode(mode);
            delay.IsEnabled = mode == OpenMode.Hover;
        };

        delay.Header = Strings.L("Hover delay");
        delay.Minimum = 0;
        delay.Maximum = 1500;
        delay.StepFrequency = 50;
        delay.TickFrequency = 250;
        delay.Value = settings.HoverDelay;
        delay.IsEnabled = settings.OpenMode == OpenMode.Hover;
        delay.ThumbToolTipValueConverter = new MillisecondsConverter();
        delay.ValueChanged += (_, e) => island.SetHoverDelay((int)e.NewValue);

        stack.Children.Add(Card(
            open,
            delay,
            Hint(Strings.L("Move your pointer to the top edge of the screen and the drop appears."))));

        stack.Children.Add(Header(Strings.L("Appearance")));
        var theme = new RadioButtons { Header = Strings.L("Theme") };
        theme.Items.Add(Strings.L("Smoke Glass"));
        theme.Items.Add(Strings.L("OLED Black"));
        theme.SelectedIndex = settings.Theme == IslandTheme.Smoke ? 0 : 1;
        theme.SelectionChanged += (_, _) => island.SetTheme(theme.SelectedIndex == 0 ? IslandTheme.Smoke : IslandTheme.Oled);
        stack.Children.Add(Card(theme));

        stack.Children.Add(Header(Strings.L("System")));
        var replace = new ToggleSwitch { Header = Strings.L("Replace the Windows volume pop-up"), IsOn = settings.ReplaceVolumeFlyout };
        replace.Toggled += (_, _) => island.SetReplaceVolume(replace.IsOn);
        var login = new ToggleSwitch { Header = Strings.L("Launch at login"), IsOn = Settings.LaunchAtLogin };
        login.Toggled += (_, _) => Settings.LaunchAtLogin = login.IsOn;
        stack.Children.Add(Card(replace, login));

        stack.Children.Add(Header(Strings.L("About")));
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var about = new TextBlock
        {
            Text = string.Format(Strings.L("Version {0}"), version == null ? "" : $"{version.Major}.{version.Minor}.{version.Build}"),
            Opacity = 0.8,
        };
        var link = new HyperlinkButton
        {
            Content = Strings.L("FreeNotch on GitHub"),
            NavigateUri = new Uri("https://github.com/werquess/FreeNotch"),
            Padding = new Thickness(0),
        };
        stack.Children.Add(Card(about, link));

        Content = new ScrollViewer { Content = stack, RequestedTheme = ElementTheme.Dark };
    }

    private static TextBlock Header(string text) => new()
    {
        Text = text,
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(2, 14, 0, 2),
    };

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Opacity = 0.7,
        TextWrapping = TextWrapping.Wrap,
    };

    private static Border Card(params UIElement[] children)
    {
        var panel = new StackPanel { Spacing = 16 };
        foreach (var child in children) panel.Children.Add(child);
        return new Border
        {
            Child = panel,
            Padding = new Thickness(18, 16, 18, 18),
            CornerRadius = new CornerRadius(8),
            Background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush,
            BorderBrush = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush,
            BorderThickness = new Thickness(1),
        };
    }

    private sealed class MillisecondsConverter : Microsoft.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) => $"{System.Convert.ToInt32(value)} ms";

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
    }
}
