using System.Runtime.InteropServices;

namespace FreeNotch;

internal static class Native
{
    public const int GWL_EXSTYLE = -20;
    public const int GWL_STYLE = -16;
    public const long WS_POPUP = 0x80000000L;
    public const long WS_CAPTION = 0x00C00000L;
    public const long WS_THICKFRAME = 0x00040000L;
    public const long WS_SYSMENU = 0x00080000L;
    public const long WS_BORDER = 0x00800000L;
    public const long WS_DLGFRAME = 0x00400000L;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP_HOOK = 0x0202;
    public const long WS_EX_TOOLWINDOW = 0x00000080;
    public const long WS_EX_APPWINDOW = 0x00040000;

    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_BORDER_COLOR = 34;
    public const int DWMWCP_DONOTROUND = 1;
    public const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;
    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;
    public const uint VK_VOLUME_MUTE = 0xAD;
    public const uint VK_VOLUME_DOWN = 0xAE;
    public const uint VK_VOLUME_UP = 0xAF;

    public const uint WM_APP = 0x8000;
    public const uint WM_COMMAND = 0x0111;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_CONTEXTMENU = 0x007B;
    public const uint NIN_SELECT = 0x0400;
    public const int NIM_ADD = 0;
    public const int NIM_MODIFY = 1;
    public const int NIM_DELETE = 2;
    public const int NIM_SETVERSION = 4;
    public const int NIF_MESSAGE = 0x1;
    public const int NIF_ICON = 0x2;
    public const int NIF_TIP = 0x4;
    public const int NOTIFYICON_VERSION_4 = 4;
    public const uint MF_STRING = 0x0;
    public const uint MF_CHECKED = 0x8;
    public const uint MF_SEPARATOR = 0x800;
    public const uint MF_POPUP = 0x10;
    public const uint TPM_RETURNCMD = 0x100;
    public const uint TPM_RIGHTBUTTON = 0x2;
    public const uint IMAGE_ICON = 1;
    public const uint LR_LOADFROMFILE = 0x10;
    public static readonly IntPtr HWND_MESSAGE = new(-3);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);

    [DllImport("user32.dll")]
    public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool redraw);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    public static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    public static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    public static extern bool DestroyMenu(IntPtr hMenu);

    public static void SetToolWindow(IntPtr hwnd)
    {
        var style = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        style = (style | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));
    }

    public static void RemoveFrame(IntPtr hwnd)
    {
        var style = (long)GetWindowLongPtr(hwnd, GWL_STYLE);
        style = (style & ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_BORDER | WS_DLGFRAME)) | WS_POPUP;
        SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(style));
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        var corner = DWMWCP_DONOTROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
        var border = DWMWA_COLOR_NONE;
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
    }
}
