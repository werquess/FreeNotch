using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FreeNotch;

public sealed class TabbedCard : Grid
{
    public const float CardWidth = 580;
    private const double StripHeight = 40;
    private const double BottomPadding = 14;

    private readonly List<IslandTab> tabs;
    private readonly List<Button> buttons = new();
    private readonly Grid host = new();
    private IslandTab current;
    private bool active;

    public event Action<Vector2>? Resized;

    public TabbedCard(TabContext context)
    {
        tabs = new List<IslandTab>
        {
            new MediaTab(context),
            new LyricsTab(context),
            new ShelfTab(context),
            new ClipboardTab(context),
            new NotesTab(context),
            new FocusTab(context),
            new SystemTab(context),
            new ToolsTab(context),
        };
        context.RequestTab = Select;

        RowDefinitions.Add(new RowDefinition { Height = new GridLength(StripHeight) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var strip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Padding = new Thickness(16, 8, 16, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        foreach (var tab in tabs)
        {
            var button = new Button
            {
                Content = Ui.Icon(tab.Glyph, 14, 200),
                Width = 34,
                Height = 28,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
            };
            ToolTipService.SetToolTip(button, tab.Title);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, tab.Title);
            var target = tab;
            button.Click += (_, _) => Select(target);
            buttons.Add(button);
            strip.Children.Add(button);

            tab.Visibility = Visibility.Collapsed;
            host.Children.Add(tab);
        }
        SetRow(strip, 0);
        SetRow(host, 1);
        Children.Add(strip);
        Children.Add(host);

        current = tabs[0];
        current.Visibility = Visibility.Visible;
        Highlight();
    }

    public Vector2 CardSize => SizeOf(current);

    public float MaxHeight => tabs.Max(t => SizeOf(t).Y);

    private static Vector2 SizeOf(IslandTab tab) => new(CardWidth, (float)(StripHeight + tab.ContentHeight + BottomPadding));

    public string CurrentId => current.Id;

    public void Select(string id)
    {
        var tab = tabs.FirstOrDefault(t => t.Id == id);
        if (tab != null) Select(tab);
    }

    private void Select(IslandTab tab)
    {
        if (tab == current) return;
        if (active) current.OnHidden();
        current.Visibility = Visibility.Collapsed;
        current = tab;
        current.Visibility = Visibility.Visible;
        Highlight();
        if (active) current.OnShown();
        Resized?.Invoke(CardSize);
    }

    private void Highlight()
    {
        for (var i = 0; i < tabs.Count; i++)
        {
            var on = tabs[i] == current;
            buttons[i].Background = on ? Ui.White(30) : new SolidColorBrush(Colors.Transparent);
            if (buttons[i].Content is FontIcon icon) icon.Foreground = Ui.White(on ? (byte)255 : (byte)150);
        }
    }

    public void SetActive(bool on)
    {
        if (on == active) return;
        active = on;
        if (on) current.OnShown();
        else current.OnHidden();
    }
}
