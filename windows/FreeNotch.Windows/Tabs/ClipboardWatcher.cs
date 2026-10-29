using System.Runtime.InteropServices;

namespace FreeNotch;

internal sealed class ClipboardWatcher : IDisposable
{
    private const uint WmClipboardUpdate = 0x031D;
    private const string WindowClassName = "FreeNotchClipboardWatcher";
    private static readonly IntPtr MessageOnlyParent = new IntPtr(-3);

    private delegate IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClassEx
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WindowClassEx windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height,
        IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    private readonly WindowProcedure procedure;
    private IntPtr hwnd;
    private bool listening;

    public ClipboardWatcher()
    {
        procedure = HandleMessage;
        var instance = GetModuleHandle(null);
        var windowClass = new WindowClassEx
        {
            cbSize = (uint)Marshal.SizeOf<WindowClassEx>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(procedure),
            hInstance = instance,
            lpszClassName = WindowClassName,
        };

        RegisterClassEx(ref windowClass);
        hwnd = CreateWindowEx(0, WindowClassName, WindowClassName, 0, 0, 0, 0, 0, MessageOnlyParent, IntPtr.Zero, instance, IntPtr.Zero);
        if (hwnd != IntPtr.Zero) listening = AddClipboardFormatListener(hwnd);
    }

    public Action? Changed { get; set; }

    private IntPtr HandleMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmClipboardUpdate)
        {
            Changed?.Invoke();
            return IntPtr.Zero;
        }

        return DefWindowProc(window, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (hwnd == IntPtr.Zero) return;
        if (listening) RemoveClipboardFormatListener(hwnd);
        listening = false;
        DestroyWindow(hwnd);
        hwnd = IntPtr.Zero;
    }
}
