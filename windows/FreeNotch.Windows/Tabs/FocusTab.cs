using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FreeNotch;

public sealed class FocusTab : IslandTab
{
    private const int MinFocusMinutes = 5;
    private const int MaxFocusMinutes = 90;
    private const int ShortBreakMinutes = 5;
    private const int LongBreakMinutes = 15;
    private const int SessionsBeforeLongBreak = 4;
    private const int MaxVisibleLaps = 3;
    private const double DigitWidth = 31;
    private const double ColonWidth = 15;
    private const double RingSize = 112;
    private const double RingStroke = 7;

    private static readonly SolidColorBrush Orange = new(Windows.UI.Color.FromArgb(255, 255, 159, 10));
    private static readonly SolidColorBrush Mint = new(Windows.UI.Color.FromArgb(255, 99, 230, 190));

    private static readonly Dictionary<string, Dictionary<string, string>> Text = new()
    {
        ["tr"] = new()
        {
            ["Focus"] = "Odak", ["Break"] = "Mola", ["Pomodoro"] = "Pomodoro", ["Stopwatch"] = "Kronometre",
            ["Start"] = "Başlat", ["Pause"] = "Duraklat", ["Resume"] = "Sürdür", ["Stop"] = "Durdur",
            ["Lap"] = "Tur", ["Reset"] = "Sıfırla", ["Session"] = "Seans", ["min"] = "dk",
            ["Focus length"] = "Odak süresi", ["Laps"] = "Turlar", ["No laps yet"] = "Henüz tur yok",
        },
        ["de"] = new()
        {
            ["Focus"] = "Fokus", ["Break"] = "Pause", ["Pomodoro"] = "Pomodoro", ["Stopwatch"] = "Stoppuhr",
            ["Start"] = "Start", ["Pause"] = "Anhalten", ["Resume"] = "Fortsetzen", ["Stop"] = "Stopp",
            ["Lap"] = "Runde", ["Reset"] = "Zurücksetzen", ["Session"] = "Sitzung", ["min"] = "Min.",
            ["Focus length"] = "Fokusdauer", ["Laps"] = "Runden", ["No laps yet"] = "Noch keine Runden",
        },
        ["es"] = new()
        {
            ["Focus"] = "Enfoque", ["Break"] = "Descanso", ["Pomodoro"] = "Pomodoro", ["Stopwatch"] = "Cronómetro",
            ["Start"] = "Iniciar", ["Pause"] = "Pausar", ["Resume"] = "Reanudar", ["Stop"] = "Detener",
            ["Lap"] = "Vuelta", ["Reset"] = "Reiniciar", ["Session"] = "Sesión", ["min"] = "min",
            ["Focus length"] = "Duración", ["Laps"] = "Vueltas", ["No laps yet"] = "Sin vueltas",
        },
        ["fr"] = new()
        {
            ["Focus"] = "Concentration", ["Break"] = "Pause", ["Pomodoro"] = "Pomodoro", ["Stopwatch"] = "Chronomètre",
            ["Start"] = "Démarrer", ["Pause"] = "Suspendre", ["Resume"] = "Reprendre", ["Stop"] = "Arrêter",
            ["Lap"] = "Tour", ["Reset"] = "Réinitialiser", ["Session"] = "Session", ["min"] = "min",
            ["Focus length"] = "Durée", ["Laps"] = "Tours", ["No laps yet"] = "Aucun tour",
        },
    };

    private static string T(string key) => Strings.Pick(Text, key);

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint type);

    private readonly string settingsPath = Path.Combine(TabContext.DataFolder, "focus.json");
    private readonly DispatcherQueueTimer refreshTimer;
    private readonly DispatcherQueueTimer phaseTimer;

    private readonly StackPanel digitsRow = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock phaseLabel = Ui.Text("", 12, FontWeights.SemiBold, 140);
    private readonly TextBlock primaryText = Ui.Text("", 12, FontWeights.SemiBold);
    private readonly GlyphIcon primaryIcon = Ui.Icon("play", 10);
    private readonly TextBlock secondaryText = Ui.Text("", 12, FontWeights.SemiBold);
    private readonly Button secondaryButton;
    private readonly Button pomodoroModeButton;
    private readonly Button stopwatchModeButton;
    private readonly Microsoft.UI.Xaml.Shapes.Path ringArc = new()
    {
        StrokeThickness = RingStroke,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
    };
    private readonly GlyphIcon ringIcon = Ui.Icon("timer", 18);
    private readonly TextBlock ringText = Ui.Text("", 11, FontWeights.SemiBold, 150);
    private readonly StackPanel pomodoroSide = new() { Spacing = 8 };
    private readonly StackPanel stopwatchSide = new() { Spacing = 6 };
    private readonly TextBlock focusMinutesText = Ui.Text("", 15, FontWeights.SemiBold);
    private readonly TextBlock breakInfoText = Ui.Text("", 11, FontWeights.Normal, 120);
    private readonly StackPanel sessionDots = new() { Orientation = Orientation.Horizontal, Spacing = 5 };
    private readonly TextBlock noLaps = Ui.Text("", 12, FontWeights.Normal, 110);
    private readonly List<(Grid Row, TextBlock Name, TextBlock Time)> lapRows = new();

    private bool shown;
    private bool stopwatchMode;
    private int focusMinutes = 25;

    private bool onBreak;
    private int completedSessions;
    private bool pomodoroRunning;
    private DateTime pomodoroEnd;
    private TimeSpan pomodoroRemaining;
    private TimeSpan pomodoroPhaseLength;

    private bool stopwatchRunning;
    private DateTime stopwatchStart;
    private TimeSpan stopwatchAccumulated;
    private readonly List<TimeSpan> lapTotals = new();

    public FocusTab(TabContext context) : base(context)
    {
        Padding = new Thickness(22, 4, 22, 6);
        LoadSettings();
        PreparePhase(false);

        refreshTimer = context.Queue.CreateTimer();
        refreshTimer.Interval = TimeSpan.FromMilliseconds(200);
        refreshTimer.IsRepeating = true;
        refreshTimer.Tick += (_, _) => Refresh();

        phaseTimer = context.Queue.CreateTimer();
        phaseTimer.IsRepeating = false;
        phaseTimer.Tick += (_, _) =>
        {
            phaseTimer.Stop();
            if (pomodoroRunning) EndPhase();
        };

        pomodoroModeButton = MakeModeButton(T("Pomodoro"), () => SetMode(false));
        stopwatchModeButton = MakeModeButton(T("Stopwatch"), () => SetMode(true));
        secondaryButton = MakeButton(secondaryText, null, false, OnSecondary);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(RingSize + 8) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(172) });

        var switchPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        switchPanel.Children.Add(pomodoroModeButton);
        switchPanel.Children.Add(stopwatchModeButton);
        var track = new Border
        {
            Child = switchPanel,
            Padding = new Thickness(3),
            CornerRadius = new CornerRadius(15),
            Background = Ui.White(16),
            BorderBrush = Ui.White(20),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8),
        };
        SetRow(track, 0);
        SetColumnSpan(track, 3);
        Children.Add(track);

        var ring = BuildRing();
        SetRow(ring, 1);
        SetColumn(ring, 0);
        Children.Add(ring);

        var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 2 };
        phaseLabel.HorizontalAlignment = HorizontalAlignment.Center;
        center.Children.Add(digitsRow);
        center.Children.Add(phaseLabel);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
        };
        buttons.Children.Add(MakeButton(primaryText, primaryIcon, true, OnPrimary));
        buttons.Children.Add(secondaryButton);
        center.Children.Add(buttons);
        SetRow(center, 1);
        SetColumn(center, 1);
        Children.Add(center);

        BuildPomodoroSide();
        BuildStopwatchSide();
        var side = new Grid();
        side.Children.Add(pomodoroSide);
        side.Children.Add(stopwatchSide);
        var panel = new Border
        {
            Child = side,
            Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(18),
            Background = Ui.White(13),
            BorderBrush = Ui.White(24),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 2),
        };
        SetRow(panel, 1);
        SetColumn(panel, 2);
        Children.Add(panel);

        ApplyMode();
        Refresh();
    }

    public override string Id => "focus";
    public override string Glyph => "timer";
    public override string Title => T("Focus");
    public override double ContentHeight => 184;

    public override void OnShown()
    {
        shown = true;
        Refresh();
        refreshTimer.Start();
    }

    public override void OnHidden()
    {
        shown = false;
        refreshTimer.Stop();
    }

    private static Button MakeButton(TextBlock label, GlyphIcon? icon, bool primary, Action action)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (icon != null)
        {
            icon.VerticalAlignment = VerticalAlignment.Center;
            content.Children.Add(icon);
        }
        content.Children.Add(label);
        if (primary)
        {
            var ink = new SolidColorBrush(Microsoft.UI.Colors.Black);
            label.Foreground = ink;
            if (icon != null) icon.Foreground = ink;
        }
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(16, 7, 16, 7),
            MinWidth = 92,
            CornerRadius = new CornerRadius(16),
            Background = primary ? Ui.White(240) : Ui.White(24),
            BorderBrush = primary ? null : Ui.White(22),
            BorderThickness = new Thickness(primary ? 0 : 1),
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button MakeModeButton(string title, Action action)
    {
        var button = new Button
        {
            Content = Ui.Text(title, 12, FontWeights.SemiBold),
            Padding = new Thickness(14, 4, 14, 5),
            MinHeight = 0,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(0),
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button MakeStepButton(string sign, Action action)
    {
        var label = Ui.Text(sign, 15, FontWeights.SemiBold);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.Margin = new Thickness(0, -2, 0, 0);
        var button = new Button
        {
            Content = label,
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(14),
            Background = Ui.White(26),
            BorderThickness = new Thickness(0),
        };
        button.Click += (_, _) => action();
        return button;
    }

    private FrameworkElement BuildRing()
    {
        var host = new Grid { Width = RingSize, Height = RingSize, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        host.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Stroke = Ui.White(26),
            StrokeThickness = RingStroke,
            Margin = new Thickness(0),
        });
        host.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Fill = Ui.White(8),
            Margin = new Thickness(RingStroke + 5),
        });
        host.Children.Add(ringArc);
        var label = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        ringIcon.HorizontalAlignment = HorizontalAlignment.Center;
        ringText.HorizontalAlignment = HorizontalAlignment.Center;
        label.Children.Add(ringIcon);
        label.Children.Add(ringText);
        host.Children.Add(label);
        return host;
    }

    private void SetRing(double fraction, Brush brush)
    {
        ringArc.Stroke = brush;
        ringIcon.Foreground = brush;
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction <= 0.002)
        {
            ringArc.Data = null;
            return;
        }
        var c = RingSize / 2;
        var r = c - RingStroke / 2;
        var angle = Math.Min(fraction, 0.9995) * 2 * Math.PI;
        var figure = new PathFigure { StartPoint = new Windows.Foundation.Point(c, c - r), IsClosed = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Windows.Foundation.Point(c + r * Math.Sin(angle), c - r * Math.Cos(angle)),
            Size = new Windows.Foundation.Size(r, r),
            IsLargeArc = angle > Math.PI,
            SweepDirection = SweepDirection.Clockwise,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        ringArc.Data = geometry;
    }

    private static TextBlock Caption(string text)
    {
        var caption = Ui.Text(text, 11, FontWeights.SemiBold, 125);
        caption.CharacterSpacing = 20;
        return caption;
    }

    private void BuildPomodoroSide()
    {
        var stepper = new Grid();
        stepper.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        stepper.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        stepper.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var minus = MakeStepButton("−", () => AdjustFocusMinutes(-5));
        var plus = MakeStepButton("+", () => AdjustFocusMinutes(5));
        focusMinutesText.VerticalAlignment = VerticalAlignment.Center;
        focusMinutesText.HorizontalAlignment = HorizontalAlignment.Center;
        SetColumn(minus, 0);
        SetColumn(focusMinutesText, 1);
        SetColumn(plus, 2);
        stepper.Children.Add(minus);
        stepper.Children.Add(focusMinutesText);
        stepper.Children.Add(plus);

        pomodoroSide.Children.Add(Caption(T("Focus length")));
        pomodoroSide.Children.Add(stepper);
        pomodoroSide.Children.Add(new Border { Height = 1, Background = Ui.White(18), Margin = new Thickness(0, 2, 0, 0) });
        pomodoroSide.Children.Add(breakInfoText);
        for (var i = 0; i < SessionsBeforeLongBreak; i++)
        {
            sessionDots.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 7, Height = 7 });
        }
        pomodoroSide.Children.Add(sessionDots);
    }

    private void BuildStopwatchSide()
    {
        stopwatchSide.Children.Add(Caption(T("Laps")));
        noLaps.Text = T("No laps yet");
        noLaps.Margin = new Thickness(0, 4, 0, 0);
        stopwatchSide.Children.Add(noLaps);
        for (var i = 0; i < MaxVisibleLaps; i++)
        {
            var row = new Grid();
            var name = Ui.Text("", 12, FontWeights.Normal, 150);
            var time = Ui.Text("", 12, FontWeights.SemiBold, 235);
            time.FontFamily = new FontFamily("Segoe UI Variable Display");
            time.HorizontalAlignment = HorizontalAlignment.Right;
            row.Children.Add(name);
            row.Children.Add(time);
            lapRows.Add((row, name, time));
            stopwatchSide.Children.Add(row);
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(settingsPath)) return;
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            if (document.RootElement.TryGetProperty("focusMinutes", out var value) && value.TryGetInt32(out var minutes))
                focusMinutes = Math.Clamp(minutes, MinFocusMinutes, MaxFocusMinutes);
        }
        catch
        {
        }
    }

    private void SaveSettings()
    {
        try
        {
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(new Dictionary<string, int> { ["focusMinutes"] = focusMinutes }));
        }
        catch
        {
        }
    }

    private int CurrentBreakMinutes() =>
        completedSessions > 0 && completedSessions % SessionsBeforeLongBreak == 0 ? LongBreakMinutes : ShortBreakMinutes;

    private void PreparePhase(bool isBreak)
    {
        onBreak = isBreak;
        pomodoroRunning = false;
        pomodoroPhaseLength = TimeSpan.FromMinutes(isBreak ? CurrentBreakMinutes() : focusMinutes);
        pomodoroRemaining = pomodoroPhaseLength;
    }

    private void AdjustFocusMinutes(int delta)
    {
        var updated = Math.Clamp(focusMinutes + delta, MinFocusMinutes, MaxFocusMinutes);
        if (updated == focusMinutes) return;
        var untouchedFocus = !onBreak && !pomodoroRunning && pomodoroRemaining == pomodoroPhaseLength;
        focusMinutes = updated;
        if (untouchedFocus) PreparePhase(false);
        SaveSettings();
        Refresh();
    }

    private void SetMode(bool stopwatch)
    {
        stopwatchMode = stopwatch;
        ApplyMode();
        Refresh();
    }

    private void ApplyMode()
    {
        var ink = new SolidColorBrush(Microsoft.UI.Colors.Black);
        pomodoroModeButton.Background = stopwatchMode ? Ui.White(0) : Ui.White(235);
        stopwatchModeButton.Background = stopwatchMode ? Ui.White(235) : Ui.White(0);
        ((TextBlock)pomodoroModeButton.Content).Foreground = stopwatchMode ? Ui.White(150) : ink;
        ((TextBlock)stopwatchModeButton.Content).Foreground = stopwatchMode ? ink : Ui.White(150);
        pomodoroSide.Visibility = stopwatchMode ? Visibility.Collapsed : Visibility.Visible;
        stopwatchSide.Visibility = stopwatchMode ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPrimary()
    {
        if (stopwatchMode)
        {
            if (stopwatchRunning)
            {
                stopwatchAccumulated += DateTime.UtcNow - stopwatchStart;
                stopwatchRunning = false;
            }
            else
            {
                stopwatchStart = DateTime.UtcNow;
                stopwatchRunning = true;
            }
        }
        else if (pomodoroRunning)
        {
            pomodoroRemaining = pomodoroEnd - DateTime.UtcNow;
            if (pomodoroRemaining < TimeSpan.Zero) pomodoroRemaining = TimeSpan.Zero;
            pomodoroRunning = false;
            phaseTimer.Stop();
        }
        else
        {
            StartPomodoro();
        }
        PublishLive();
        Refresh();
    }

    private void OnSecondary()
    {
        if (stopwatchMode)
        {
            if (stopwatchRunning)
            {
                lapTotals.Add(StopwatchElapsed());
            }
            else
            {
                stopwatchAccumulated = TimeSpan.Zero;
                lapTotals.Clear();
            }
        }
        else
        {
            phaseTimer.Stop();
            completedSessions = 0;
            PreparePhase(false);
        }
        PublishLive();
        Refresh();
    }

    private void StartPomodoro()
    {
        if (pomodoroRemaining <= TimeSpan.Zero) pomodoroRemaining = pomodoroPhaseLength;
        pomodoroEnd = DateTime.UtcNow + pomodoroRemaining;
        pomodoroRunning = true;
        FocusLive.PomodoroEndsUtc = pomodoroEnd;
        phaseTimer.Interval = pomodoroRemaining;
        phaseTimer.Start();
    }

    private void EndPhase()
    {
        phaseTimer.Stop();
        MessageBeep(0x40);
        if (onBreak)
        {
            PreparePhase(false);
        }
        else
        {
            completedSessions++;
            PreparePhase(true);
        }
        PublishLive();
        Context.OpenTab?.Invoke(Id);
        if (shown) Refresh();
    }

    private void PublishLive()
    {
        FocusLive.PomodoroRunning = pomodoroRunning;
        FocusLive.PomodoroOnBreak = onBreak;
        FocusLive.PomodoroEndsUtc = pomodoroEnd;
        FocusLive.StopwatchRunning = stopwatchRunning;
        FocusLive.StopwatchStartUtc = stopwatchStart;
        FocusLive.StopwatchAccumulated = stopwatchAccumulated;
        FocusLive.Raise();
    }

    private TimeSpan StopwatchElapsed() =>
        stopwatchRunning ? stopwatchAccumulated + (DateTime.UtcNow - stopwatchStart) : stopwatchAccumulated;

    private static string FormatClock(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        var totalSeconds = (long)value.TotalSeconds;
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds / 60 % 60;
        var seconds = totalSeconds % 60;
        return hours > 0 ? $"{hours}:{minutes:00}:{seconds:00}" : $"{minutes:00}:{seconds:00}";
    }

    private static string FormatLap(TimeSpan value)
    {
        var tenths = (int)(value.Milliseconds / 100);
        return $"{FormatClock(value)}.{tenths}";
    }

    private void SetDigits(string text)
    {
        while (digitsRow.Children.Count < text.Length)
        {
            digitsRow.Children.Add(new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Variable Display"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 50,
                TextAlignment = TextAlignment.Center,
                Foreground = Ui.White(),
            });
        }
        while (digitsRow.Children.Count > text.Length)
            digitsRow.Children.RemoveAt(digitsRow.Children.Count - 1);

        for (var i = 0; i < text.Length; i++)
        {
            var cell = (TextBlock)digitsRow.Children[i];
            var character = text[i].ToString();
            if (cell.Text != character) cell.Text = character;
            cell.Width = text[i] == ':' ? ColonWidth : DigitWidth;
        }
    }

    private void Refresh()
    {
        if (!stopwatchMode && pomodoroRunning && pomodoroEnd - DateTime.UtcNow <= TimeSpan.Zero)
        {
            EndPhase();
            return;
        }

        if (stopwatchMode) RefreshStopwatch();
        else RefreshPomodoro();
    }

    private void RefreshPomodoro()
    {
        var remaining = pomodoroRunning ? pomodoroEnd - DateTime.UtcNow : pomodoroRemaining;
        SetDigits(FormatClock(remaining));
        var session = onBreak ? completedSessions : completedSessions + 1;
        phaseLabel.Text = $"{T(onBreak ? "Break" : "Focus")}  ·  {T("Session")} {session}";
        primaryText.Text = pomodoroRunning ? T("Pause") : pomodoroRemaining < pomodoroPhaseLength ? T("Resume") : T("Start");
        primaryIcon.Glyph = pomodoroRunning ? "pause" : "play";
        secondaryText.Text = T("Reset");
        focusMinutesText.Text = $"{focusMinutes} {T("min")}";
        breakInfoText.Text = $"{T("Break")} {ShortBreakMinutes} / {LongBreakMinutes} {T("min")}";

        var length = pomodoroPhaseLength.TotalSeconds;
        var done = length > 0 ? 1 - Math.Max(remaining.TotalSeconds, 0) / length : 0;
        var accent = onBreak ? Mint : Orange;
        SetRing(done, accent);
        ringIcon.Glyph = onBreak ? "coffee" : "timer";
        ringText.Text = $"{(int)Math.Round(done * 100)}%";

        var filled = completedSessions % SessionsBeforeLongBreak;
        if (onBreak && filled == 0 && completedSessions > 0) filled = SessionsBeforeLongBreak;
        for (var i = 0; i < sessionDots.Children.Count; i++)
        {
            ((Microsoft.UI.Xaml.Shapes.Ellipse)sessionDots.Children[i]).Fill = i < filled ? Orange : Ui.White(40);
        }
    }

    private void RefreshStopwatch()
    {
        var elapsed = StopwatchElapsed();
        SetDigits(FormatClock(elapsed));
        phaseLabel.Text = T("Stopwatch");
        primaryText.Text = stopwatchRunning ? T("Stop") : T("Start");
        primaryIcon.Glyph = stopwatchRunning ? "pause" : "play";
        secondaryText.Text = stopwatchRunning ? T("Lap") : T("Reset");

        SetRing(elapsed.TotalSeconds % 60 / 60, Ui.White(235));
        ringIcon.Glyph = "timer";
        ringText.Text = lapTotals.Count > 0 ? $"{T("Lap")} {lapTotals.Count + (stopwatchRunning ? 1 : 0)}" : $"{(int)(elapsed.TotalSeconds % 60):00}s";

        noLaps.Visibility = lapTotals.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        for (var i = 0; i < lapRows.Count; i++)
        {
            var (row, name, time) = lapRows[i];
            var lapIndex = lapTotals.Count - 1 - i;
            if (lapIndex < 0)
            {
                row.Visibility = Visibility.Collapsed;
                continue;
            }
            var previous = lapIndex > 0 ? lapTotals[lapIndex - 1] : TimeSpan.Zero;
            row.Visibility = Visibility.Visible;
            name.Text = $"{T("Lap")} {lapIndex + 1}";
            time.Text = FormatLap(lapTotals[lapIndex] - previous);
        }
    }
}

public static class FocusLive
{
    public static bool PomodoroRunning { get; internal set; }
    public static bool PomodoroOnBreak { get; internal set; }
    public static DateTime PomodoroEndsUtc { get; internal set; }
    public static bool StopwatchRunning { get; internal set; }
    internal static DateTime StopwatchStartUtc { get; set; }
    internal static TimeSpan StopwatchAccumulated { get; set; }

    public static TimeSpan StopwatchElapsed =>
        StopwatchRunning ? StopwatchAccumulated + (DateTime.UtcNow - StopwatchStartUtc) : StopwatchAccumulated;

    public static event Action? Changed;

    internal static void Raise() => Changed?.Invoke();
}
