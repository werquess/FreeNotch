using System.Numerics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace FreeNotch;

public sealed class IslandWindow : Window
{
    private enum Phase { Hidden, Peek, Activity, Hud, Opening, Card, Closing }

    private static readonly Vector2 Arc = new(210, 20);
    private const float CardRadius = 28;
    private const float Gap = 10;
    private const float ShadowPad = 34;
    private const float SmallBottom = 14;
    private const float SmallEar = 6;
    private const float HudSide = 96;
    private const float TriggerHalf = 150;
    private const float PeekHalf = 160;
    private const float PeekDepth = 64;
    private const float Slack = 28;

    private readonly Settings settings;
    private readonly MediaService media;
    private readonly VolumeService volume = new();
    private readonly MediaKeyHook keys = new();
    private readonly MouseHook mouse = new();
    private readonly ForegroundWatcher foreground = new();
    private readonly IslandView view = new();
    private readonly HudContent hud = new();
    private readonly CompactContent compact;
    private readonly TabbedCard card;
    private readonly IntPtr hwnd;
    private readonly DispatcherQueue queue;
    private readonly DispatcherQueueTimer hudTimer;
    private readonly DispatcherQueueTimer dwellTimer;
    private readonly DispatcherQueueTimer leaveTimer;
    private readonly DispatcherQueueTimer shrinkTimer;

    private Phase phase = Phase.Hidden;
    private Activity activity = Activity.None;
    private NotchShape shape;
    private RectInt32 screen;
    private int anchorX;
    private double scale = 1;
    private bool pointerInside;
    private bool held;

    public Action? OpenSettings { get; set; }

    public IslandWindow(Settings settings, MediaService media)
    {
        this.settings = settings;
        this.media = media;
        queue = DispatcherQueue;

        SystemBackdrop = new WinUIEx.TransparentTintBackdrop();
        view.RequestedTheme = ElementTheme.Dark;
        Content = view;
        Title = "FreeNotch";

        hwnd = WindowNative.GetWindowHandle(this);
        var context = new TabContext(settings, media, queue, hwnd)
        {
            HoldOpen = on =>
            {
                held = on;
                if (!on && !pointerInside) leaveTimer.Start();
            },
            RequestClose = Close,
            OpenTab = OpenTab,
            OpenSettings = () => OpenSettings?.Invoke(),
        };
        card = new TabbedCard(context);
        card.Resized += OnCardResized;
        card.Margin = new Thickness(0, Gap, 0, 0);
        compact = new CompactContent(media);

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        Native.SetToolWindow(hwnd);
        Native.MakeFrameless(hwnd);
        Native.AcceptFiles(hwnd, paths => queue.TryEnqueue(() => card.AddFiles(paths)));

        view.AddLayer(compact);
        view.AddLayer(hud);
        view.AddLayer(card);
        view.ApplyTheme(settings.Theme);

        hudTimer = Timer(1600, HideHud);
        dwellTimer = Timer(0, () => { if (phase is Phase.Peek or Phase.Activity && pointerInside) Open(); });
        leaveTimer = Timer(0, OnLeft);
        shrinkTimer = Timer(700, () => { if (phase is not (Phase.Opening or Phase.Closing)) Clip(Footprint(shape)); });

        view.PointerPressed += (_, _) => { if (phase is Phase.Peek or Phase.Hud or Phase.Activity) Open(); };
        view.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Escape) Close(); };
        Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated && phase == Phase.Card && !held) Close();
        };

        media.Changed += () =>
        {
            compact.RefreshMedia();
            Refresh();
        };
        FocusLive.Changed += Refresh;
        foreground.Changed += () => queue.TryEnqueue(Refresh);
        keys.Handler = OnMediaKey;
        if (settings.ReplaceVolumeFlyout) keys.Start();
        mouse.Moved = OnMouse;
    }

    private DispatcherQueueTimer Timer(int ms, Action tick)
    {
        var timer = queue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(ms);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => tick();
        return timer;
    }

    public bool IsOpen => phase is Phase.Card or Phase.Opening;

    private float NotchWidth => Math.Clamp(settings.NotchWidth, 120, 320);
    private float NotchHeight => Math.Clamp(settings.NotchHeight, 24, 44);

    private NotchShape ActivityShape(Activity a) => new(NotchWidth + CompactContent.Side(a) * 2 + 20, NotchHeight, SmallBottom, SmallEar);
    private NotchShape HudShape => new(NotchWidth + HudSide * 2, NotchHeight, SmallBottom, SmallEar);
    private NotchShape CardShape => new(TabbedCard.CardWidth, card.CardSize.Y, CardRadius, 0, Gap);

    private static Vector2 Footprint(NotchShape s) => s.Floating
        ? new(s.Width + ShadowPad * 2, s.Gap + s.Height + ShadowPad)
        : new(s.Width + s.Ear * 2 + 4, s.Height + 4);

    public void Start()
    {
        Locate();
        mouse.Start();
        var intro = Timer(500, () =>
        {
            Refresh();
            if (phase != Phase.Hidden) return;
            Peek(false);
            var back = Timer(1500, () => { if (phase == Phase.Peek && !pointerInside) Retract(); });
            back.Start();
        });
        intro.Start();
    }

    private void Locate()
    {
        screen = DisplayArea.Primary.OuterBounds;
        scale = Native.GetDpiForWindow(hwnd) / 96.0;
        anchorX = screen.X + screen.Width / 2;
    }

    private Vector2 Room => new(TabbedCard.CardWidth * 1.03f + ShadowPad * 2, Gap + card.MaxHeight * 1.04f + ShadowPad + 8);

    private void ShowWindow()
    {
        var room = Room;
        var w = (int)Math.Ceiling(room.X * scale);
        var h = (int)Math.Ceiling(room.Y * scale);
        var target = new RectInt32(anchorX - w / 2, screen.Y, w, h);
        var pos = AppWindow.Position;
        var size = AppWindow.Size;
        if (pos.X != target.X || pos.Y != target.Y || size.Width != target.Width || size.Height != target.Height)
        {
            AppWindow.MoveAndResize(target);
        }
        if (!AppWindow.IsVisible)
        {
            AppWindow.Show(false);
            Native.RemoveFrame(hwnd);
        }
    }

    private void Clip(Vector2 area)
    {
        var w = (int)Math.Ceiling(Room.X * scale);
        var cw = Math.Min((int)Math.Ceiling(area.X * scale), w);
        var ch = (int)Math.Ceiling(area.Y * scale);
        var x = (w - cw) / 2;
        Native.SetWindowRgn(hwnd, Native.CreateRoundRectRgn(x, 0, x + cw + 1, ch + 1, 0, 0), true);
    }

    private void MorphTo(NotchShape next)
    {
        var grow = Vector2.Max(Footprint(shape), Footprint(next)) * 1.04f;
        shape = next;
        ShowWindow();
        Clip(grow);
        view.Morph(next);
        shrinkTimer.Stop();
        shrinkTimer.Start();
    }

    private void HideWindow()
    {
        shape = default;
        compact.Hide();
        view.Reset();
        AppWindow.Hide();
    }

    private Activity CurrentActivity()
    {
        if (settings.ActivityTimers && FocusLive.PomodoroRunning) return Activity.Pomodoro;
        if (settings.ActivityTimers && FocusLive.StopwatchRunning) return Activity.Stopwatch;
        if (settings.ActivityMedia && media.HasSession && media.IsPlaying && !foreground.Matches(media.SourceId)) return Activity.Media;
        return Activity.None;
    }

    private void Refresh()
    {
        var next = CurrentActivity();
        switch (phase)
        {
            case Phase.Hidden:
            case Phase.Peek:
            case Phase.Activity:
                if (next == Activity.None)
                {
                    if (phase == Phase.Activity) Rest();
                    return;
                }
                if (phase == Phase.Activity && next == activity) return;
                ShowActivity(next);
                return;
            default:
                return;
        }
    }

    private void ShowActivity(Activity next)
    {
        Locate();
        dwellTimer.Stop();
        leaveTimer.Stop();
        if (phase == Phase.Peek) view.Reset();
        activity = next;
        phase = Phase.Activity;
        pointerInside = false;
        var s = ActivityShape(next);
        compact.Show(next, NotchWidth);
        MorphTo(s);
        view.ShowLayer(compact, new Vector2(s.Width, s.Height), 120);
    }

    private void Rest()
    {
        var next = CurrentActivity();
        if (next != Activity.None)
        {
            ShowActivity(next);
            return;
        }
        activity = Activity.None;
        phase = Phase.Closing;
        compact.Hide();
        view.ShowLayer(null, Vector2.Zero, 0);
        view.Vanish(() =>
        {
            if (phase != Phase.Closing) return;
            phase = Phase.Hidden;
            HideWindow();
        });
    }

    private void OnCardResized(Vector2 size)
    {
        if (phase is not (Phase.Card or Phase.Opening)) return;
        MorphTo(CardShape);
        view.ResizeLayer(card, size);
    }

    private void OpenTab(string id)
    {
        card.Select(id);
        if (phase is not (Phase.Card or Phase.Opening)) Open();
    }

    private void OnMouse(int x, int y)
    {
        var dx = Math.Abs(x - anchorX);
        var dy = y - screen.Y;
        bool inside;
        switch (phase)
        {
            case Phase.Hidden:
                var dragging = mouse.LeftDown;
                if (dy <= (dragging ? 4 : 1) && dy >= -2 && dx <= TriggerHalf * scale && x >= screen.X && x < screen.X + screen.Width)
                {
                    queue.TryEnqueue(() =>
                    {
                        if (phase != Phase.Hidden) return;
                        if (dragging) OpenTab("shelf");
                        else Peek(true);
                    });
                }
                return;
            case Phase.Peek:
                inside = dx <= PeekHalf * scale && dy <= PeekDepth * scale && dy >= -2;
                if (inside && mouse.LeftDown) queue.TryEnqueue(() => OpenTab("shelf"));
                break;
            case Phase.Activity:
                inside = dx <= (shape.Width / 2 + shape.Ear + 6) * scale && dy <= (shape.Height + 8) * scale && dy >= -2;
                if (inside && mouse.LeftDown) queue.TryEnqueue(() => OpenTab("shelf"));
                break;
            case Phase.Card:
            case Phase.Opening:
                if (settings.OpenMode != OpenMode.Hover) return;
                inside = dx <= (shape.Width / 2 + Slack) * scale && dy <= (shape.Gap + shape.Height + Slack) * scale && dy >= -2;
                break;
            default:
                return;
        }
        if (inside == pointerInside) return;
        pointerInside = inside;
        queue.TryEnqueue(() =>
        {
            if (pointerInside)
            {
                leaveTimer.Stop();
                if (phase == Phase.Activity && settings.OpenMode == OpenMode.Hover)
                {
                    dwellTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(settings.HoverDelay, 0, 3000));
                    dwellTimer.Start();
                }
            }
            else
            {
                if (phase == Phase.Activity) dwellTimer.Stop();
                leaveTimer.Interval = TimeSpan.FromMilliseconds(phase == Phase.Peek ? 220 : 450);
                leaveTimer.Start();
            }
        });
    }

    private void OnLeft()
    {
        if (pointerInside) return;
        if (phase == Phase.Peek) Retract();
        else if (held) return;
        else if (phase == Phase.Card && settings.OpenMode == OpenMode.Hover) Close();
    }

    private void Peek(bool byPointer)
    {
        if (phase != Phase.Hidden) return;
        Locate();
        phase = Phase.Peek;
        pointerInside = byPointer;
        hudTimer.Stop();
        ShowWindow();
        Clip(new Vector2(PeekHalf * 2 + 20, PeekDepth + 8));
        var hover = byPointer && settings.OpenMode == OpenMode.Hover;
        view.Peek(Arc, hover ? settings.HoverDelay : 0);
        dwellTimer.Stop();
        if (hover)
        {
            dwellTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(settings.HoverDelay, 0, 3000) + 420);
            dwellTimer.Start();
        }
    }

    private void Retract()
    {
        if (phase != Phase.Peek) return;
        phase = Phase.Closing;
        dwellTimer.Stop();
        view.Retract(Arc, () =>
        {
            if (phase != Phase.Closing) return;
            phase = Phase.Hidden;
            HideWindow();
            Refresh();
        });
    }

    public void Open()
    {
        if (phase is Phase.Card or Phase.Opening) return;
        dwellTimer.Stop();
        leaveTimer.Stop();
        hudTimer.Stop();
        Locate();
        var fromNotch = phase is Phase.Activity or Phase.Hud && shape.Height > 0;
        if (!fromNotch && phase != Phase.Peek)
        {
            view.Reset();
            ShowWindow();
            view.Peek(Arc, 0);
        }
        compact.Hide();
        phase = Phase.Opening;
        pointerInside = true;
        var target = CardShape;
        card.SetActive(true);
        view.ShowLayer(null, Vector2.Zero, 0);
        ShowWindow();
        Clip(Room);
        shape = target;
        view.Fall(Arc, target, Opened);
        view.ShowLayer(card, card.CardSize, 480);
        shrinkTimer.Stop();
    }

    private void Opened()
    {
        if (phase != Phase.Opening) return;
        phase = Phase.Card;
        Clip(Footprint(shape));
        TakeFocus();
    }

    // Taking focus in the middle of a file drag would fight the drag source, so wait for the drop.
    private void TakeFocus()
    {
        if (phase != Phase.Card) return;
        if (mouse.LeftDown)
        {
            Timer(150, TakeFocus).Start();
            return;
        }
        Activate();
        Native.SetForegroundWindow(hwnd);
    }

    public void Close()
    {
        if (phase != Phase.Card) return;
        leaveTimer.Stop();
        held = false;
        card.SetActive(false);
        var next = CurrentActivity();
        if (next != Activity.None)
        {
            phase = Phase.Closing;
            view.ShowLayer(null, Vector2.Zero, 0);
            Clip(Room);
            view.Absorb(ActivityShape(next), () =>
            {
                if (phase != Phase.Closing) return;
                pointerInside = false;
                var now = CurrentActivity();
                phase = Phase.Hidden;
                if (now != Activity.None)
                {
                    ShowActivity(now);
                    return;
                }
                phase = Phase.Activity;
                Rest();
            });
            return;
        }
        phase = Phase.Closing;
        view.ShowLayer(null, Vector2.Zero, 0);
        Clip(Room);
        view.Drain(Arc, () =>
        {
            if (phase != Phase.Closing) return;
            phase = Phase.Hidden;
            pointerInside = false;
            HideWindow();
            Refresh();
        });
    }

    public void Toggle()
    {
        if (phase == Phase.Card) Close();
        else Open();
    }

    private bool OnMediaKey(uint vk, bool down)
    {
        if (!settings.ReplaceVolumeFlyout) return false;
        if (!down) return true;
        var direction = vk switch
        {
            Native.VK_VOLUME_UP => 1,
            Native.VK_VOLUME_DOWN => -1,
            _ => 0,
        };
        queue.TryEnqueue(() =>
        {
            var (level, muted) = volume.Change(direction);
            hud.Update(level, muted);
            ShowHud();
        });
        return true;
    }

    private void ShowHud()
    {
        if (phase is Phase.Card or Phase.Opening) return;
        if (phase != Phase.Hud)
        {
            Locate();
            dwellTimer.Stop();
            leaveTimer.Stop();
            if (phase == Phase.Peek) view.Reset();
            compact.Hide();
            phase = Phase.Hud;
            var s = HudShape;
            MorphTo(s);
            view.ShowLayer(hud, new Vector2(s.Width, s.Height), 80);
        }
        hudTimer.Stop();
        hudTimer.Start();
    }

    private void HideHud()
    {
        if (phase != Phase.Hud) return;
        Rest();
    }

    public void ApplyNotchSettings()
    {
        card.ApplyNotch();
        if (phase == Phase.Activity) ShowActivity(activity);
        else Refresh();
    }

    public void SetReplaceVolume(bool on)
    {
        settings.ReplaceVolumeFlyout = on;
        settings.Save();
        if (on) keys.Start();
        else keys.Stop();
    }

    public void SetTheme(IslandTheme theme)
    {
        settings.Theme = theme;
        settings.Save();
        view.ApplyTheme(theme);
    }

    public void SetOpenMode(OpenMode mode)
    {
        settings.OpenMode = mode;
        settings.Save();
    }

    public void SetHoverDelay(int ms)
    {
        settings.HoverDelay = ms;
        settings.Save();
    }

    public void Shutdown()
    {
        keys.Dispose();
        mouse.Dispose();
        foreground.Dispose();
    }
}
