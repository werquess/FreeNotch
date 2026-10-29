using System.Numerics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace FreeNotch;

public sealed class IslandWindow : Window
{
    private enum Phase { Hidden, Peek, Opening, Card, Closing, Hud }

    private static readonly Vector2 Arc = new(210, 20);
    private static readonly Vector2 HudSize = new(260, 36);
    private const float CardRadius = 34;
    private const float HudRadius = 18;
    private const float TopGap = 12;
    private const float TriggerHalf = 150;
    private const float PeekHalf = 160;
    private const float PeekDepth = 64;
    private const float CardSlack = 28;

    private readonly Settings settings;
    private readonly MediaService media;
    private readonly VolumeService volume = new();
    private readonly MediaKeyHook keys = new();
    private readonly MouseHook mouse = new();
    private readonly IslandView view = new();
    private readonly HudContent hud = new();
    private readonly TabbedCard card;
    private readonly IntPtr hwnd;
    private readonly DispatcherQueue queue;
    private readonly DispatcherQueueTimer hudTimer;
    private readonly DispatcherQueueTimer dwellTimer;
    private readonly DispatcherQueueTimer leaveTimer;

    private Phase phase = Phase.Hidden;
    private RectInt32 screen;
    private int anchorX;
    private double scale = 1;
    private bool pointerInside;
    private bool held;
    private Vector2 cardSize;

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
            HoldOpen = on => held = on,
            RequestClose = Close,
            OpenTab = OpenTab,
        };
        card = new TabbedCard(context);
        cardSize = card.CardSize;
        card.Resized += OnCardResized;
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

        view.AddLayer(hud);
        view.AddLayer(card);
        view.ApplyTheme(settings.Theme);

        hudTimer = queue.CreateTimer();
        hudTimer.Interval = TimeSpan.FromMilliseconds(1600);
        hudTimer.IsRepeating = false;
        hudTimer.Tick += (_, _) => HideHud();

        dwellTimer = queue.CreateTimer();
        dwellTimer.IsRepeating = false;
        dwellTimer.Tick += (_, _) => { if (phase == Phase.Peek && pointerInside) Open(); };

        leaveTimer = queue.CreateTimer();
        leaveTimer.IsRepeating = false;
        leaveTimer.Tick += (_, _) => OnLeft();

        view.PointerPressed += (_, _) => { if (phase is Phase.Peek or Phase.Hud) Open(); };
        view.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Escape) Close(); };
        Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated && phase == Phase.Card && !held) Close();
        };

        keys.Handler = OnMediaKey;
        if (settings.ReplaceVolumeFlyout) keys.Start();
        mouse.Moved = OnMouse;
    }

    public bool IsOpen => phase is Phase.Card or Phase.Opening;

    public void Start()
    {
        Locate();
        mouse.Start();
        Preview(500);
    }

    private void Preview(int delayMs)
    {
        var intro = queue.CreateTimer();
        intro.Interval = TimeSpan.FromMilliseconds(delayMs);
        intro.IsRepeating = false;
        intro.Tick += (_, _) =>
        {
            if (phase != Phase.Hidden) return;
            Peek(false);
            var back = queue.CreateTimer();
            back.Interval = TimeSpan.FromMilliseconds(1500);
            back.IsRepeating = false;
            back.Tick += (_, _) => { if (phase == Phase.Peek && !pointerInside) Retract(); };
            back.Start();
        };
        intro.Start();
    }

    private void Locate()
    {
        var display = DisplayArea.Primary;
        screen = display.OuterBounds;
        scale = Native.GetDpiForWindow(hwnd) / 96.0;
        anchorX = screen.X + screen.Width / 2;
    }

    private Vector2 Room => new(TabbedCard.CardWidth * 1.06f + 24, TopGap + card.MaxHeight + 28);

    private void PlaceRoom()
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
    }

    private void Clip(Vector2 area)
    {
        var w = (int)Math.Ceiling(Room.X * scale);
        var cw = (int)Math.Ceiling(area.X * scale);
        var ch = (int)Math.Ceiling(area.Y * scale);
        var x = (w - cw) / 2;
        var region = Native.CreateRoundRectRgn(x, 0, x + cw + 1, ch + 1, 0, 0);
        Native.SetWindowRgn(hwnd, region, true);
    }

    private void ShowWindow()
    {
        PlaceRoom();
        if (!AppWindow.IsVisible)
        {
            AppWindow.Show(false);
            Native.RemoveFrame(hwnd);
        }
    }

    private void OnCardResized(Vector2 size)
    {
        cardSize = size;
        if (phase is not (Phase.Card or Phase.Opening)) return;
        view.Morph(size);
        view.ResizeLayer(card, size);
        Clip(Room);
    }

    private void OpenTab(string id)
    {
        card.Select(id);
        if (phase is not (Phase.Card or Phase.Opening)) Open();
    }

    private void HideWindow()
    {
        view.Reset();
        AppWindow.Hide();
    }

    private void OnMouse(int x, int y)
    {
        var dx = Math.Abs(x - anchorX);
        var dy = y - screen.Y;
        bool inside;
        switch (phase)
        {
            case Phase.Hidden:
                if (dy <= 1 && dy >= -2 && dx <= TriggerHalf * scale && x >= screen.X && x < screen.X + screen.Width)
                {
                    var dragging = mouse.LeftDown;
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
                break;
            case Phase.Card:
            case Phase.Opening:
                if (settings.OpenMode != OpenMode.Hover) return;
                inside = dx <= (cardSize.X / 2 + CardSlack) * scale && dy <= (TopGap + cardSize.Y + CardSlack) * scale && dy >= -2;
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
            }
            else
            {
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
            dwellTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(settings.HoverDelay, 0, 3000) + 380);
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
        });
    }

    public void Open()
    {
        if (phase is Phase.Card or Phase.Opening) return;
        if (phase is Phase.Hidden or Phase.Closing or Phase.Hud)
        {
            Locate();
            view.Reset();
            phase = Phase.Hidden;
            ShowWindow();
            view.Peek(Arc, 0);
        }
        dwellTimer.Stop();
        leaveTimer.Stop();
        hudTimer.Stop();
        phase = Phase.Opening;
        pointerInside = true;
        cardSize = card.CardSize;
        Clip(Room);
        view.SetLayerTop(TopGap);
        card.SetActive(true);
        view.ShowLayer(null, cardSize, 0);
        view.Open(Arc, TopGap, cardSize, CardRadius, () =>
        {
            if (phase != Phase.Opening) return;
            phase = Phase.Card;
            Activate();
            Native.SetForegroundWindow(hwnd);
        });
        var reveal = queue.CreateTimer();
        reveal.Interval = TimeSpan.FromMilliseconds(640);
        reveal.IsRepeating = false;
        reveal.Tick += (_, _) => { if (phase is Phase.Opening or Phase.Card) view.ShowLayer(card, cardSize, 0); };
        reveal.Start();
    }

    public void Close()
    {
        if (phase != Phase.Card) return;
        phase = Phase.Closing;
        leaveTimer.Stop();
        held = false;
        card.SetActive(false);
        view.ShowLayer(null, cardSize, 0);
        view.Close(Arc, TopGap, () =>
        {
            if (phase != Phase.Closing) return;
            phase = Phase.Hidden;
            pointerInside = false;
            HideWindow();
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
            phase = Phase.Hud;
            ShowWindow();
            Clip(new Vector2(HudSize.X * 1.1f + 20, HudSize.Y + 12));
            view.NotchIn(HudSize, HudRadius);
            view.SetLayerTop(0);
            view.ShowLayer(hud, HudSize, 60);
        }
        hudTimer.Stop();
        hudTimer.Start();
    }

    private void HideHud()
    {
        if (phase != Phase.Hud) return;
        phase = Phase.Closing;
        view.ShowLayer(null, HudSize, 0);
        view.NotchOut(HudSize, HudRadius, () =>
        {
            if (phase != Phase.Closing) return;
            phase = Phase.Hidden;
            HideWindow();
        });
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
    }
}
