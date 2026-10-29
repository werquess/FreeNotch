using System.Runtime.InteropServices;

namespace FreeNotch;

public sealed class TrayIcon : IDisposable
{
    public sealed record Item(string Title, Action? Action = null, bool Checked = false, IReadOnlyList<Item>? Children = null)
    {
        public static readonly Item Separator = new("-");
    }

    private const uint CallbackMessage = Native.WM_APP + 1;
    private readonly Native.WndProc wndProc;
    private readonly IntPtr window;
    private readonly IntPtr icon;
    private Native.NOTIFYICONDATA data;
    private readonly Dictionary<uint, Action> actions = new();

    public Func<IReadOnlyList<Item>>? MenuBuilder { get; set; }
    public Action? Clicked { get; set; }

    public TrayIcon(string tooltip, string iconPath)
    {
        wndProc = WindowProc;
        var instance = Native.GetModuleHandle(null);
        var cls = new Native.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<Native.WNDCLASSEX>(),
            lpfnWndProc = wndProc,
            hInstance = instance,
            lpszClassName = "FreeNotchTray",
        };
        Native.RegisterClassEx(ref cls);
        window = Native.CreateWindowEx(0, "FreeNotchTray", "FreeNotchTray", 0, 0, 0, 0, 0, Native.HWND_MESSAGE, IntPtr.Zero, instance, IntPtr.Zero);
        icon = Native.LoadImage(IntPtr.Zero, iconPath, Native.IMAGE_ICON, 32, 32, Native.LR_LOADFROMFILE);

        data = new Native.NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<Native.NOTIFYICONDATA>(),
            hWnd = window,
            uID = 1,
            uFlags = Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP,
            uCallbackMessage = CallbackMessage,
            hIcon = icon,
            szTip = tooltip,
            szInfo = "",
            szInfoTitle = "",
            uVersion = Native.NOTIFYICON_VERSION_4,
        };
        Native.Shell_NotifyIcon(Native.NIM_ADD, ref data);
        Native.Shell_NotifyIcon(Native.NIM_SETVERSION, ref data);
    }

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == CallbackMessage)
        {
            var evt = (uint)(lParam.ToInt64() & 0xFFFF);
            if (evt is Native.WM_LBUTTONUP or Native.NIN_SELECT)
            {
                Clicked?.Invoke();
            }
            else if (evt is Native.WM_RBUTTONUP or Native.WM_CONTEXTMENU)
            {
                ShowMenu();
            }
            return IntPtr.Zero;
        }
        return Native.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        var items = MenuBuilder?.Invoke();
        if (items == null) return;
        actions.Clear();
        uint nextId = 100;
        var menu = Build(items, ref nextId);
        Native.GetCursorPos(out var point);
        Native.SetForegroundWindow(window);
        var command = Native.TrackPopupMenu(menu, Native.TPM_RETURNCMD | Native.TPM_RIGHTBUTTON, point.X, point.Y, 0, window, IntPtr.Zero);
        Native.DestroyMenu(menu);
        if (command != 0 && actions.TryGetValue(command, out var action)) action();
    }

    private IntPtr Build(IReadOnlyList<Item> items, ref uint nextId)
    {
        var menu = Native.CreatePopupMenu();
        foreach (var item in items)
        {
            if (item.Title == "-")
            {
                Native.AppendMenu(menu, Native.MF_SEPARATOR, UIntPtr.Zero, null);
            }
            else if (item.Children != null)
            {
                var sub = Build(item.Children, ref nextId);
                Native.AppendMenu(menu, Native.MF_POPUP, (UIntPtr)(ulong)sub.ToInt64(), item.Title);
            }
            else
            {
                var id = nextId++;
                if (item.Action != null) actions[id] = item.Action;
                Native.AppendMenu(menu, Native.MF_STRING | (item.Checked ? Native.MF_CHECKED : 0), (UIntPtr)id, item.Title);
            }
        }
        return menu;
    }

    public void Dispose()
    {
        Native.Shell_NotifyIcon(Native.NIM_DELETE, ref data);
        if (icon != IntPtr.Zero) Native.DestroyIcon(icon);
        Native.DestroyWindow(window);
    }
}
