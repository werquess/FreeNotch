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
    private const int MaxVisibleLaps = 4;
    private const double DigitWidth = 34;
    private const double ColonWidth = 16;

    private static readonly Dictionary<string, Dictionary<string, string>> Text = new()
    {
        ["tr"] = new()
        {
            ["Focus"] = "Odak", ["Break"] = "Mola", ["Pomodoro"] = "Pomodoro", ["Stopwatch"] = "Kronometre",
            ["Start"] = "Başlat", ["Pause"] = "Duraklat", ["Resume"] = "Sürdür", ["Stop"] = "Durdur",
            ["Lap"] = "Tur", ["Reset"] = "Sıfırla", ["Session"] = "Seans", ["min"] = "dk",
        },
        ["de"] = new()
        {
            ["Focus"] = "Fokus", ["Break"] = "Pause", ["Pomodoro"] = "Pomodoro", ["Stopwatch"] = "Stoppuhr",
            ["Start"] = "Start", ["Pause"] = "Anhalten", ["Resume"] = "Fortsetzen", ["Stop"] = "Stopp",
            ["Lap"] = "Runde", ["Reset"] = "Zurücksetzen", ["Session"] = "Sitzung", ["min"] = "Min.",
        },
        ["es"] = new()
        {
            ["Focus"] = "Enfoque", ["Break"] = "Descanso", ["Pomodoro"] = "Pomodoro", ["Stopwatch"] = "Cronómetro",
            ["Start"] = "Iniciar", ["Pause"] = "Pausar", ["Resume"] = "Reanudar", ["Stop"] = "Detener",
            ["Lap"] = "Vuelta", ["Reset"] = "Reiniciar", ["Session"] = "Sesión", ["min"] = "min",
        },
        ["fr"] = new()
        {
            ["Focus"] = "Concentration", ["Break"] = "Pause", ["Pomodoro"] = "Pomodoro", ["Stopwatch"] = "Chronomètre",
            ["Start"] = "Démarrer", ["Pause"] = "Suspendre", ["Resume"] = "Reprendre", ["Stop"] = "Arrêter",
            ["Lap"] = "Tour", ["Reset"] = "Réinitialiser", ["Session"] = "Session", ["min"] = "min",
        },
    };

    private static string T(string key) => Strings.Pick(Text, key);

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint type);

    private readonly string settingsPath = Path.Combine(TabContext.DataFolder, "focus.json");
    private readonly DispatcherQueueTimer refreshTimer;
    private readonly DispatcherQueueTimer phaseTimer;

    private readonly StackPanel digitsRow = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock phaseLabel = Ui.Text("", 12, FontWeights.SemiBold, 150);
    private readonly TextBlock primaryText = Ui.Text("", 12, FontWeights.SemiBold);
    private readonly TextBlock secondaryText = Ui.Text("", 12, FontWeights.SemiBold);
    private readonly Button secondaryButton;
    private readonly Button pomodoroModeButton;
    private readonly Button stopwatchModeButton;
    private readonly StackPanel pomodoroSide = new() { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel stopwatchSide = new() { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock focusMinutesText = Ui.Text("", 14, FontWeights.SemiBold);
    private readonly TextBlock breakInfoText = Ui.Text("", 11, FontWeights.Normal, 120);
    private readonly List<TextBlock> lapLines = new();

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
        secondaryButton = MakeButton(secondaryText, OnSecondary);

        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

        var switchPanel = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Width = 104 };
        switchPanel.Children.Add(pomodoroModeButton);
        switchPanel.Children.Add(stopwatchModeButton);
        SetColumn(switchPanel, 0);
        Children.Add(switchPanel);

        var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 2 };
        phaseLabel.HorizontalAlignment = HorizontalAlignment.Center;
        center.Children.Add(digitsRow);
        center.Children.Add(phaseLabel);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
        };
        buttons.Children.Add(MakeButton(primaryText, OnPrimary));
        buttons.Children.Add(secondaryButton);
        center.Children.Add(buttons);
        SetColumn(center, 1);
        Children.Add(center);

        BuildPomodoroSide();
        BuildStopwatchSide();
        SetColumn(pomodoroSide, 2);
        SetColumn(stopwatchSide, 2);
        Children.Add(pomodoroSide);
        Children.Add(stopwatchSide);

        ApplyMode();
        Refresh();
    }

    public override string Id => "focus";
    public override string Glyph => "";
    public override string Title => T("Focus");
    public override double ContentHeight => 170;

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

    private static Button MakeButton(TextBlock label, Action action)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(14, 6, 14, 6),
            MinWidth = 76,
            CornerRadius = new CornerRadius(10),
            Background = Ui.White(30),
            BorderThickness = new Thickness(0),
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button MakeModeButton(string title, Action action)
    {
        var button = new Button
        {
            Content = Ui.Text(title, 12, FontWeights.SemiBold),
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(0),
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button MakeStepButton(string glyph, Action action)
    {
        var button = Ui.IconButton(glyph, 11, 26, action);
        button.Background = Ui.White(30);
        return button;
    }

    private void BuildPomodoroSide()
    {
        var stepper = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        stepper.Children.Add(MakeStepButton("", () => AdjustFocusMinutes(-5)));
        stepper.Children.Add(focusMinutesText);
        stepper.Children.Add(MakeStepButton("", () => AdjustFocusMinutes(5)));
        focusMinutesText.VerticalAlignment = VerticalAlignment.Center;
        focusMinutesText.MinWidth = 44;
        focusMinutesText.TextAlignment = TextAlignment.Center;

        var caption = Ui.Text(T("Focus"), 11, FontWeights.Normal, 120);
        caption.HorizontalAlignment = HorizontalAlignment.Right;
        breakInfoText.HorizontalAlignment = HorizontalAlignment.Right;

        pomodoroSide.HorizontalAlignment = HorizontalAlignment.Right;
        pomodoroSide.Children.Add(caption);
        pomodoroSide.Children.Add(stepper);
        pomodoroSide.Children.Add(breakInfoText);
    }

    private void BuildStopwatchSide()
    {
        stopwatchSide.HorizontalAlignment = HorizontalAlignment.Right;
        for (var i = 0; i < MaxVisibleLaps; i++)
        {
            var line = Ui.Text("", 12, FontWeights.Normal, 190);
            line.HorizontalAlignment = HorizontalAlignment.Right;
            lapLines.Add(line);
            stopwatchSide.Children.Add(line);
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
        pomodoroModeButton.Background = stopwatchMode ? Ui.White(0) : Ui.White(45);
        stopwatchModeButton.Background = stopwatchMode ? Ui.White(45) : Ui.White(0);
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
        Refresh();
    }

    private void StartPomodoro()
    {
        if (pomodoroRemaining <= TimeSpan.Zero) pomodoroRemaining = pomodoroPhaseLength;
        pomodoroEnd = DateTime.UtcNow + pomodoroRemaining;
        pomodoroRunning = true;
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
        Context.OpenTab?.Invoke(Id);
        if (shown) Refresh();
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
                FontWeight = FontWeights.Bold,
                FontSize = 56,
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
        secondaryText.Text = T("Reset");
        focusMinutesText.Text = $"{focusMinutes} {T("min")}";
        breakInfoText.Text = $"{T("Break")} {ShortBreakMinutes} / {LongBreakMinutes} {T("min")}";
    }

    private void RefreshStopwatch()
    {
        SetDigits(FormatClock(StopwatchElapsed()));
        phaseLabel.Text = T("Stopwatch");
        primaryText.Text = stopwatchRunning ? T("Stop") : T("Start");
        secondaryText.Text = stopwatchRunning ? T("Lap") : T("Reset");

        for (var i = 0; i < lapLines.Count; i++)
        {
            var lapIndex = lapTotals.Count - 1 - i;
            if (lapIndex < 0)
            {
                lapLines[i].Text = "";
                continue;
            }
            var previous = lapIndex > 0 ? lapTotals[lapIndex - 1] : TimeSpan.Zero;
            lapLines[i].Text = $"{T("Lap")} {lapIndex + 1}   {FormatLap(lapTotals[lapIndex] - previous)}";
        }
    }
}
