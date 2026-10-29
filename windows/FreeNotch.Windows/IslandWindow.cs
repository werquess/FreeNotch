using System.Numerics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using WinRT.Interop;

namespace FreeNotch;

public sealed class IslandWindow : Window
{
    private enum Phase { Dropping, Pill, Hud, Card }

    private static readonly Vector2 IdlePill = new(118, 30);
    private static readonly Vector2 MediaPill = new(196, 32);
    private static readonly Vector2 HudPill = new(250, 36);
    private static readonly Vector2 CardSize = new(580, 184);
    private const float CardRadius = 34;
    private const float TopGap = 12;

    private readonly Settings settings;
    private readonly MediaService media;
    private readonly VolumeService volume = new();
    private readonly MediaKeyHook keys = new();
    private readonly IslandView view = new();
    private readonly PillContent pill = new();
    private readonly HudContent hud = new();
    private readonly MediaCard card;
    private readonly IntPtr hwnd;
    private readonly DispatcherQueue queue;
    private readonly DispatcherQueueTimer hudTimer;
    private readonly DispatcherQueueTimer shrinkTimer;

    private Phase phase = Phase.Dropping;
    private Vector2 target = IdlePill;
    private PointInt32 anchor;
    private bool growsUp;
    private bool pressed;
    private bool dragging;
    private Native.POINT pressCursor;
    private PointInt32 pressWindow;

    public IslandWindow(Settings settings, MediaService media)
    {
        this.settings = settings;
        this.media = media;
        queue = DispatcherQueue;
        card = new MediaCard(media);

        SystemBackdrop = new WinUIEx.TransparentTintBackdrop();
        view.RequestedTheme = ElementTheme.Dark;
        Content = view;
        Title = "FreeNotch";

        hwnd = WindowNative.GetWindowHandle(this);
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        Native.SetToolWindow(hwnd);
        Native.RemoveFrame(hwnd);

        view.AddLayer(pill);
        view.AddLayer(hud);
        view.AddLayer(card);
        view.ApplyTheme(settings.Theme);

        hudTimer = queue.CreateTimer();
        hudTimer.Interval = TimeSpan.FromMilliseconds(1600);
        hudTimer.IsRepeating = false;
        hudTimer.Tick += (_, _) => { if (phase == Phase.Hud) GoTo(Phase.Pill); };

        shrinkTimer = queue.CreateTimer();
        shrinkTimer.Interval = TimeSpan.FromMilliseconds(650);
        shrinkTimer.IsRepeating = false;
        shrinkTimer.Tick += (_, _) => { if (phase != Phase.Dropping) Place(target); };

        view.PointerPressed += OnPressed;
        view.PointerMoved += OnMoved;
        view.PointerReleased += OnReleased;
        view.PointerCaptureLost += (_, _) => pressed = dragging = false;
        view.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Escape && phase == Phase.Card) GoTo(Phase.Pill); };
        Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated && phase == Phase.Card) GoTo(Phase.Pill);
        };

        media.Changed += OnMediaChanged;
        keys.Handler = OnMediaKey;
        if (settings.ReplaceVolumeFlyout) keys.Start();
    }

    private double Scale => Native.GetDpiForWindow(hwnd) / 96.0;

    private Vector2 PillSize => media.HasSession && media.IsPlaying ? MediaPill : IdlePill;

    public bool IsOpen => phase == Phase.Card;

    public void Start()
    {
        LoadAnchor();
        var display = DisplayArea.GetFromPoint(anchor, DisplayAreaFallback.Nearest);
        var outer = display.OuterBounds;
        var s = Scale;
        var size = PillSize;
        target = size;
        var bottom = growsUp ? anchor.Y : anchor.Y + (int)Math.Ceiling(size.Y * s);
        var width = (int)Math.Ceiling(size.X * 1.12 * s) + 4;
        AppWindow.MoveAndResize(new RectInt32(anchor.X - width / 2, outer.Y, width, Math.Max(bottom - outer.Y, 40)));
        view.SetGrowsUp(false);
        view.SetTop(0);
        AppWindow.Show(false);

        var landTop = (float)((bottom - outer.Y) / s - size.Y);
        var delay = queue.CreateTimer();
        delay.Interval = TimeSpan.FromMilliseconds(180);
        delay.IsRepeating = false;
        delay.Tick += (_, _) => view.DropIn(landTop, size, FinishDrop);
        delay.Start();
    }

    private void FinishDrop()
    {
        phase = Phase.Pill;
        Place(target);
        view.SetGrowsUp(growsUp);
        view.SetTop(0);
        pill.Update(media);
        view.ShowLayer(pill, target, 0);
        var pillNow = PillSize;
        if (pillNow != target) GoTo(Phase.Pill);
    }

    public void Toggle()
    {
        if (phase == Phase.Dropping) return;
        GoTo(phase == Phase.Card ? Phase.Pill : Phase.Card);
    }

    private void GoTo(Phase next)
    {
        var size = next switch
        {
            Phase.Card => CardSize,
            Phase.Hud => HudPill,
            _ => PillSize,
        };
        var radius = next == Phase.Card ? CardRadius : size.Y / 2;
        var previous = target;
        var changed = next != phase || size != target;
        phase = next;
        target = size;
        if (!changed) return;

        var room = Vector2.Max(previous, size) * 1.06f + new Vector2(8, 8);
        Place(room);
        shrinkTimer.Stop();
        shrinkTimer.Start();

        view.Morph(size, radius);
        switch (next)
        {
            case Phase.Card:
                card.SetActive(true);
                card.Update();
                view.ShowLayer(card, size, 120);
                Activate();
                Native.SetForegroundWindow(hwnd);
                break;
            case Phase.Hud:
                card.SetActive(false);
                view.ShowLayer(hud, size, 40);
                break;
            default:
                card.SetActive(false);
                pill.Update(media);
                view.ShowLayer(pill, size, 60);
                break;
        }
    }

    private void Place(Vector2 size)
    {
        var s = Scale;
        var w = (int)Math.Ceiling(size.X * s) + 2;
        var h = (int)Math.Ceiling(size.Y * s) + 2;
        var x = anchor.X - w / 2;
        var y = growsUp ? anchor.Y - h : anchor.Y;
        AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
    }

    private void OnMediaChanged()
    {
        pill.Update(media);
        if (phase == Phase.Card) card.Update();
        if (phase == Phase.Pill && PillSize != target) GoTo(Phase.Pill);
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
            if (phase is Phase.Pill or Phase.Hud)
            {
                GoTo(Phase.Hud);
                hudTimer.Stop();
                hudTimer.Start();
            }
        });
        return true;
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

    public void ResetPosition()
    {
        settings.Positions.Remove(DisplayKey(DisplayArea.GetFromPoint(anchor, DisplayAreaFallback.Nearest)));
        settings.Save();
        LoadAnchor();
        view.SetGrowsUp(growsUp);
        Place(target);
    }

    public void Shutdown()
    {
        keys.Dispose();
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (phase is not (Phase.Pill or Phase.Hud)) return;
        pressed = true;
        dragging = false;
        Native.GetCursorPos(out pressCursor);
        pressWindow = AppWindow.Position;
        view.CapturePointer(e.Pointer);
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!pressed) return;
        Native.GetCursorPos(out var now);
        var dx = now.X - pressCursor.X;
        var dy = now.Y - pressCursor.Y;
        if (!dragging && Math.Abs(dx) + Math.Abs(dy) < 5) return;
        dragging = true;
        AppWindow.Move(new PointInt32(pressWindow.X + dx, pressWindow.Y + dy));
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!pressed) return;
        pressed = false;
        view.ReleasePointerCapture(e.Pointer);
        if (dragging)
        {
            dragging = false;
            FinishDrag();
        }
        else
        {
            GoTo(Phase.Card);
        }
    }

    private void FinishDrag()
    {
        var pos = AppWindow.Position;
        var size = AppWindow.Size;
        var center = new PointInt32(pos.X + size.Width / 2, pos.Y + size.Height / 2);
        var display = DisplayArea.GetFromPoint(center, DisplayAreaFallback.Nearest);
        var work = display.WorkArea;
        var outer = display.OuterBounds;
        var s = Scale;

        growsUp = center.Y > work.Y + work.Height / 2;
        var x = center.X;
        var y = growsUp ? pos.Y + size.Height : pos.Y;

        var snap = 56 * s;
        var half = size.Width / 2;
        var xs = new[] { work.X + work.Width / 2, work.X + (int)(20 * s) + half, work.X + work.Width - (int)(20 * s) - half };
        foreach (var candidate in xs)
        {
            if (Math.Abs(x - candidate) < snap) { x = candidate; break; }
        }
        var topY = outer.Y + (int)(TopGap * s);
        var bottomY = work.Y + work.Height - (int)(TopGap * s);
        if (!growsUp && Math.Abs(y - topY) < snap) y = topY;
        if (growsUp && Math.Abs(y - bottomY) < snap) y = bottomY;
        x = Math.Clamp(x, work.X + half, work.X + work.Width - half);
        y = Math.Clamp(y, outer.Y, outer.Y + outer.Height);

        anchor = new PointInt32(x, y);
        settings.Positions[DisplayKey(display)] = new[]
        {
            (anchor.X - work.X) / (double)work.Width,
            (anchor.Y - work.Y) / (double)work.Height,
        };
        settings.Save();
        view.SetGrowsUp(growsUp);
        Place(target);
    }

    private void LoadAnchor()
    {
        var display = DisplayArea.Primary;
        var work = display.WorkArea;
        var outer = display.OuterBounds;
        var s = Native.GetDpiForWindow(hwnd) / 96.0;
        if (settings.Positions.TryGetValue(DisplayKey(display), out var f) && f.Length == 2)
        {
            anchor = new PointInt32(work.X + (int)(f[0] * work.Width), work.Y + (int)(f[1] * work.Height));
            growsUp = f[1] > 0.5;
        }
        else
        {
            anchor = new PointInt32(work.X + work.Width / 2, outer.Y + (int)(TopGap * s));
            growsUp = false;
        }
    }

    private static string DisplayKey(DisplayArea display) => display.DisplayId.Value.ToString();
}
