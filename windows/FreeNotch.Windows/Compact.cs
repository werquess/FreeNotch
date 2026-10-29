using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FreeNotch;

public enum Activity
{
    None,
    Media,
    Pomodoro,
    Stopwatch,
}

public sealed class CompactContent : Grid
{
    public const float MediaSide = 40;
    public const float TimerSide = 64;

    private readonly MediaService media;
    private readonly Border artwork = Ui.Artwork(22, 6);
    private readonly AudioBars bars = new();
    private readonly GlyphIcon timerIcon = Ui.Icon("timer", 13);
    private readonly TextBlock time = Ui.Text("", 13, FontWeights.SemiBold);
    private readonly ColumnDefinition left = new();
    private readonly ColumnDefinition middle = new();
    private readonly ColumnDefinition right = new();
    private readonly DispatcherQueueTimer clock;
    private Activity activity;

    public CompactContent(MediaService media)
    {
        this.media = media;
        ColumnDefinitions.Add(left);
        ColumnDefinitions.Add(middle);
        ColumnDefinitions.Add(right);

        artwork.HorizontalAlignment = HorizontalAlignment.Left;
        artwork.VerticalAlignment = VerticalAlignment.Center;
        artwork.Margin = new Thickness(10, 0, 0, 0);
        timerIcon.HorizontalAlignment = HorizontalAlignment.Left;
        timerIcon.VerticalAlignment = VerticalAlignment.Center;
        timerIcon.Margin = new Thickness(12, 0, 0, 0);
        bars.HorizontalAlignment = HorizontalAlignment.Right;
        bars.Margin = new Thickness(0, 0, 12, 0);
        time.HorizontalAlignment = HorizontalAlignment.Right;
        time.VerticalAlignment = VerticalAlignment.Center;
        time.Margin = new Thickness(0, 0, 12, 0);
        time.FontFamily = new FontFamily("Segoe UI Variable Display");

        SetColumn(artwork, 0);
        SetColumn(timerIcon, 0);
        SetColumn(bars, 2);
        SetColumn(time, 2);
        Children.Add(artwork);
        Children.Add(timerIcon);
        Children.Add(bars);
        Children.Add(time);

        clock = DispatcherQueue.CreateTimer();
        clock.Interval = TimeSpan.FromMilliseconds(250);
        clock.Tick += (_, _) => Tick();
    }

    public static float Side(Activity activity) => activity switch
    {
        Activity.Media => MediaSide,
        Activity.Pomodoro or Activity.Stopwatch => TimerSide,
        _ => 0,
    };

    public void Show(Activity next, float notchWidth)
    {
        activity = next;
        var side = Side(next);
        left.Width = new GridLength(side + 10);
        middle.Width = new GridLength(Math.Max(notchWidth - 20, 0));
        right.Width = new GridLength(side + 10);

        var isMedia = next == Activity.Media;
        artwork.Visibility = isMedia ? Visibility.Visible : Visibility.Collapsed;
        bars.Visibility = isMedia ? Visibility.Visible : Visibility.Collapsed;
        timerIcon.Visibility = isMedia ? Visibility.Collapsed : Visibility.Visible;
        time.Visibility = isMedia ? Visibility.Collapsed : Visibility.Visible;

        if (isMedia)
        {
            Ui.SetArtwork(artwork, media.Artwork);
            bars.SetAnimating(media.IsPlaying);
            clock.Stop();
        }
        else
        {
            bars.SetAnimating(false);
            var orange = new SolidColorBrush(Color.FromArgb(255, 255, 159, 10));
            var mint = new SolidColorBrush(Color.FromArgb(255, 99, 230, 190));
            timerIcon.Foreground = next == Activity.Pomodoro ? (FocusLive.PomodoroOnBreak ? mint : orange) : Ui.White(230);
            time.Foreground = timerIcon.Foreground;
            Tick();
            clock.Start();
        }
    }

    public void Hide()
    {
        clock.Stop();
        bars.SetAnimating(false);
    }

    public void RefreshMedia()
    {
        if (activity != Activity.Media) return;
        Ui.SetArtwork(artwork, media.Artwork);
        bars.SetAnimating(media.IsPlaying);
    }

    private void Tick()
    {
        if (activity == Activity.Pomodoro)
        {
            var left = FocusLive.PomodoroEndsUtc - DateTime.UtcNow;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            time.Text = $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";
        }
        else if (activity == Activity.Stopwatch)
        {
            var t = FocusLive.StopwatchElapsed;
            time.Text = t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");
        }
    }
}
