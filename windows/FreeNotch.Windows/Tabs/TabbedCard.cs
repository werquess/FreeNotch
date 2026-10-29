using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FreeNotch;

public sealed class TabbedCard : Grid
{
    public const float CardWidth = 620;
    private const double HeaderHeight = 46;
    private const double BottomPadding = 14;

    private readonly TabContext context;
    private readonly List<IslandTab> tabs;
    private readonly List<Button> buttons = new();
    private readonly Grid host = new();
    private readonly TextBlock title = Ui.Text("", 12, FontWeights.SemiBold, 150);
    private readonly TextBlock batteryText = Ui.Text("", 11, FontWeights.SemiBold, 170);
    private readonly StackPanel battery = new() { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
    private readonly RowDefinition header = new();
    private readonly ColumnDefinition gap = new();
    private readonly ShelfTab shelf;
    private IslandTab current;
    private bool active;

    public event Action<Vector2>? Resized;

    public TabbedCard(TabContext context)
    {
        this.context = context;
        tabs = new List<IslandTab>
        {
            new MediaTab(context),
            new ShelfTab(context),
            new ClipboardTab(context),
            new NotesTab(context),
            new FocusTab(context),
            new SystemTab(context),
            new ToolsTab(context),
        };
        context.RequestTab = Select;

        RowDefinitions.Add(header);
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var bar = new Grid { Padding = new Thickness(18, 4, 16, 0) };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(gap);
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        foreach (var tab in tabs)
        {
            var button = new Button
            {
                Content = Ui.Icon(tab.Glyph, 12, 180),
                Width = 28,
                Height = 22,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(11),
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
        SetColumn(strip, 0);
        bar.Children.Add(strip);

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        title.VerticalAlignment = VerticalAlignment.Center;
        right.Children.Add(title);
        battery.Children.Add(batteryText);
        battery.Children.Add(BatteryIcon());
        right.Children.Add(battery);
        var gear = Ui.IconButton("gear-six", 11, 22, () => context.OpenSettings?.Invoke());
        gear.Opacity = 0.75;
        ToolTipService.SetToolTip(gear, Strings.L("Settings…").TrimEnd('…'));
        right.Children.Add(gear);
        SetColumn(right, 2);
        bar.Children.Add(right);

        SetRow(bar, 0);
        SetRow(host, 1);
        Children.Add(bar);
        Children.Add(host);

        shelf = tabs.OfType<ShelfTab>().First();
        Background = new SolidColorBrush(Colors.Transparent);
        AllowDrop = true;
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        DragLeave += (_, _) => shelf.HideDropZone();
        Drop += (_, e) =>
        {
            if (!ShelfTab.CarriesFiles(e)) return;
            e.Handled = true;
            shelf.AcceptDrop(e);
        };

        current = tabs[0];
        current.Visibility = Visibility.Visible;
        ApplyNotch();
        Highlight();
    }

    private GlyphIcon batteryGlyph = new();

    private FrameworkElement BatteryIcon()
    {
        batteryGlyph = Ui.Icon("battery-full", 14, 200);
        batteryGlyph.VerticalAlignment = VerticalAlignment.Center;
        return batteryGlyph;
    }

    // The card floats below the top edge as a drop, so the header no longer leaves room for a notch.
    public void ApplyNotch()
    {
        header.Height = new GridLength(HeaderHeight);
        gap.Width = new GridLength(12);
    }

    // Files dragged over any tab land on the shelf.
    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (ShelfTab.CarriesFiles(e) && current != shelf) Select(shelf);
        shelf.AcceptDrag(e);
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        Select(shelf);
        shelf.AddPaths(paths);
    }

    public Vector2 CardSize => SizeOf(current);

    public float MaxHeight => tabs.Max(t => SizeOf(t).Y);

    private Vector2 SizeOf(IslandTab tab) => new(CardWidth, (float)(HeaderHeight + tab.ContentHeight + BottomPadding));

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
            buttons[i].Background = on ? Ui.White(235) : new SolidColorBrush(Colors.Transparent);
            if (buttons[i].Content is GlyphIcon icon)
            {
                icon.Foreground = on ? new SolidColorBrush(Colors.Black) : Ui.White(180);
            }
        }
        title.Text = current.Title;
    }

    private void RefreshBattery()
    {
        if (!SystemTab.HasBattery)
        {
            battery.Visibility = Visibility.Collapsed;
            return;
        }
        var (percent, charging) = SystemTab.Battery();
        battery.Visibility = Visibility.Visible;
        batteryText.Text = $"{percent}%";
        batteryGlyph.Glyph = charging ? "lightning" : "battery-full";
    }

    public string? CurrentTabId => current.Id;

    public void SetActive(bool on)
    {
        if (on == active) return;
        active = on;
        if (on)
        {
            RefreshBattery();
            current.OnShown();
        }
        else
        {
            current.OnHidden();
        }
    }
}
