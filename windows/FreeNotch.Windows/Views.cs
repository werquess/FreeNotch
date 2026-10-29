using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace FreeNotch;

internal static class Ui
{
    public static readonly FontFamily Icons = new("Segoe Fluent Icons");

    public static SolidColorBrush White(byte alpha = 255) => new(Color.FromArgb(alpha, 255, 255, 255));

    public static TextBlock Text(string text, double size, Windows.UI.Text.FontWeight weight, byte alpha = 255) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = weight,
        Foreground = White(alpha),
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextWrapping = TextWrapping.NoWrap,
        MaxLines = 1,
    };

    public static FontIcon Icon(string glyph, double size, byte alpha = 255) => new()
    {
        Glyph = glyph,
        FontFamily = Icons,
        FontSize = size,
        Foreground = White(alpha),
    };

    public static Border Artwork(double size, double radius) => new()
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(radius),
        Background = White(28),
        Child = new Image { Stretch = Stretch.UniformToFill },
    };

    public static void SetArtwork(Border border, ImageSource? source)
    {
        if (border.Child is Image image) image.Source = source;
    }

    public static Button IconButton(string glyph, double size, double box, Action action)
    {
        var button = new Button
        {
            Content = Icon(glyph, size),
            Width = box,
            Height = box,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(box / 2),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
        };
        button.Click += (_, _) => action();
        return button;
    }

    public static string Time(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
    }
}

public sealed class AudioBars : StackPanel
{
    private readonly List<Rectangle> bars = new();
    private readonly float[] periods = { 0.46f, 0.58f, 0.38f };
    private bool animating;

    public AudioBars()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 2.5;
        VerticalAlignment = VerticalAlignment.Center;
        for (var i = 0; i < periods.Length; i++)
        {
            var bar = new Rectangle { Width = 3, Height = 13, RadiusX = 1.5, RadiusY = 1.5, Fill = Ui.White(230) };
            bars.Add(bar);
            Children.Add(bar);
            var visual = ElementCompositionPreview.GetElementVisual(bar);
            visual.CenterPoint = new Vector3(1.5f, 6.5f, 0);
            visual.Scale = new Vector3(1, 0.3f, 1);
        }
    }

    public void SetAnimating(bool on)
    {
        if (on == animating) return;
        animating = on;
        for (var i = 0; i < bars.Count; i++)
        {
            var visual = ElementCompositionPreview.GetElementVisual(bars[i]);
            if (on)
            {
                var c = visual.Compositor;
                var anim = c.CreateScalarKeyFrameAnimation();
                var ease = c.CreateCubicBezierEasingFunction(new Vector2(0.45f, 0f), new Vector2(0.55f, 1f));
                anim.InsertKeyFrame(0f, 0.3f);
                anim.InsertKeyFrame(0.5f, 1f, ease);
                anim.InsertKeyFrame(1f, 0.3f, ease);
                anim.Duration = TimeSpan.FromSeconds(periods[i] * 2);
                anim.DelayTime = TimeSpan.FromMilliseconds(i * 90);
                anim.IterationBehavior = AnimationIterationBehavior.Forever;
                visual.StartAnimation("Scale.Y", anim);
            }
            else
            {
                visual.StopAnimation("Scale.Y");
                visual.Scale = new Vector3(1, 0.3f, 1);
            }
        }
    }
}

public sealed class PillContent : Grid
{
    private readonly Border artwork = Ui.Artwork(20, 6);
    private readonly AudioBars bars = new();

    public PillContent()
    {
        Padding = new Thickness(7, 0, 14, 0);
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        artwork.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(artwork, 0);
        SetColumn(bars, 2);
        Children.Add(artwork);
        Children.Add(bars);
    }

    public void Update(MediaService media)
    {
        var visible = media.HasSession && media.IsPlaying;
        artwork.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        bars.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        Ui.SetArtwork(artwork, media.Artwork);
        bars.SetAnimating(visible);
    }
}

public sealed class HudContent : Grid
{
    private readonly FontIcon icon = Ui.Icon("", 15);
    private readonly Border fill = new() { Height = 5, CornerRadius = new CornerRadius(2.5), Background = Ui.White(), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Grid track = new() { Height = 5, VerticalAlignment = VerticalAlignment.Center };
    private const double TrackWidth = 150;

    public HudContent()
    {
        Padding = new Thickness(16, 0, 18, 0);
        ColumnSpacing = 12;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        icon.VerticalAlignment = VerticalAlignment.Center;
        track.Children.Add(new Border { CornerRadius = new CornerRadius(2.5), Background = Ui.White(48) });
        track.Children.Add(fill);
        track.Width = TrackWidth;
        SetColumn(icon, 0);
        SetColumn(track, 1);
        Children.Add(icon);
        Children.Add(track);
    }

    public void Update(float level, bool muted)
    {
        icon.Glyph = muted || level <= 0.001f ? "" : level < 0.34f ? "" : level < 0.67f ? "" : "";
        fill.Width = Math.Max(0, TrackWidth * (muted ? 0 : level));
    }
}

public sealed class MediaCard : Grid
{
    private readonly MediaService media;
    private readonly Border artwork = Ui.Artwork(112, 20);
    private readonly TextBlock title = Ui.Text("", 18, FontWeights.SemiBold);
    private readonly TextBlock artist = Ui.Text("", 13, FontWeights.Normal, 160);
    private readonly TextBlock source = Ui.Text("", 11, FontWeights.Medium, 105);
    private readonly TextBlock elapsed = Ui.Text("0:00", 11, FontWeights.SemiBold, 130);
    private readonly TextBlock remaining = Ui.Text("0:00", 11, FontWeights.SemiBold, 130);
    private readonly Grid bar = new() { Height = 14, Background = new SolidColorBrush(Colors.Transparent) };
    private readonly Border barFill = new() { Height = 4, CornerRadius = new CornerRadius(2), Background = Ui.White(235), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    private readonly FontIcon playIcon = new() { Glyph = "", FontFamily = Ui.Icons, FontSize = 18, Foreground = new SolidColorBrush(Colors.Black) };
    private readonly Grid player = new();
    private readonly StackPanel empty = new() { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherQueueTimer timer;

    public MediaCard(MediaService media)
    {
        this.media = media;
        Padding = new Thickness(22, 20, 24, 18);
        BuildPlayer();
        BuildEmpty();
        Children.Add(player);
        Children.Add(empty);
        timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(500);
        timer.Tick += (_, _) => UpdateProgress();
        bar.SizeChanged += (_, _) => UpdateProgress();
    }

    private void BuildPlayer()
    {
        player.ColumnSpacing = 20;
        player.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        player.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        artwork.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(artwork, 0);
        player.Children.Add(artwork);

        var right = new Grid { RowSpacing = 0 };
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        SetColumn(right, 1);
        player.Children.Add(right);

        var info = new StackPanel { Spacing = 2 };
        info.Children.Add(title);
        info.Children.Add(artist);
        info.Children.Add(source);
        source.Margin = new Thickness(0, 2, 0, 0);
        SetRow(info, 0);
        right.Children.Add(info);

        var progress = new Grid { ColumnSpacing = 10 };
        progress.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        progress.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        progress.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        elapsed.HorizontalAlignment = HorizontalAlignment.Right;
        elapsed.VerticalAlignment = VerticalAlignment.Center;
        remaining.VerticalAlignment = VerticalAlignment.Center;
        bar.Children.Add(new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = Ui.White(40), VerticalAlignment = VerticalAlignment.Center });
        bar.Children.Add(barFill);
        bar.PointerPressed += (_, e) =>
        {
            if (media.Duration <= TimeSpan.Zero || bar.ActualWidth <= 0) return;
            var x = e.GetCurrentPoint(bar).Position.X;
            media.Seek(TimeSpan.FromTicks((long)(media.Duration.Ticks * Math.Clamp(x / bar.ActualWidth, 0, 1))));
        };
        SetColumn(elapsed, 0);
        SetColumn(bar, 1);
        SetColumn(remaining, 2);
        progress.Children.Add(elapsed);
        progress.Children.Add(bar);
        progress.Children.Add(remaining);
        SetRow(progress, 2);
        right.Children.Add(progress);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 26, HorizontalAlignment = HorizontalAlignment.Center };
        controls.Children.Add(Ui.IconButton("", 16, 36, media.Previous));
        var play = new Button
        {
            Content = playIcon,
            Width = 44,
            Height = 44,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(22),
            Background = Ui.White(),
            BorderThickness = new Thickness(0),
        };
        play.Click += (_, _) => media.PlayPause();
        controls.Children.Add(play);
        controls.Children.Add(Ui.IconButton("", 16, 36, media.Next));
        SetRow(controls, 4);
        right.Children.Add(controls);
    }

    private void BuildEmpty()
    {
        var icon = Ui.Icon("", 26, 110);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        empty.Children.Add(icon);
        var line = Ui.Text(Strings.L("Nothing is playing"), 14, FontWeights.SemiBold, 200);
        line.HorizontalAlignment = HorizontalAlignment.Center;
        empty.Children.Add(line);
        var hint = Ui.Text(Strings.L("Play something in Spotify, a browser or any media app"), 11, FontWeights.Normal, 120);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        empty.Children.Add(hint);
    }

    public void SetActive(bool active)
    {
        if (active && media.IsPlaying) timer.Start();
        else timer.Stop();
        if (active) Update();
    }

    public void Update()
    {
        var has = media.HasSession;
        player.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        empty.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        title.Text = string.IsNullOrEmpty(media.Title) ? Strings.L("Unknown") : media.Title;
        artist.Text = media.Artist;
        source.Text = media.Source;
        Ui.SetArtwork(artwork, media.Artwork);
        playIcon.Glyph = media.IsPlaying ? "" : "";
        if (media.IsPlaying && IsHitTestVisible) timer.Start();
        else if (!media.IsPlaying) timer.Stop();
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        var duration = media.Duration;
        var position = media.Position;
        elapsed.Text = Ui.Time(position);
        remaining.Text = duration > TimeSpan.Zero ? "-" + Ui.Time(duration - position) : "";
        var fraction = duration > TimeSpan.Zero ? Math.Clamp(position.TotalSeconds / duration.TotalSeconds, 0, 1) : 0;
        barFill.Width = Math.Max(0, bar.ActualWidth * fraction);
    }
}
