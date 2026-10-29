using System.Runtime.InteropServices;

namespace FreeNotch;

internal sealed class ToolsColorPicker : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int VkEscape = 0x1B;
    private const int IdcCross = 32515;
    private const uint ImageCursor = 2;
    private const uint SpiSetCursors = 0x0057;
    private static readonly uint[] SystemCursors = { 32512, 32513, 32514, 32515, 32649, 32650, 32646, 32642, 32643, 32644, 32645 };

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct PointStruct
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookData
    {
        public PointStruct Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int idHook, HookProc proc, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? name);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out PointStruct point);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr dc, int x, int y);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursorW(IntPtr instance, IntPtr name);

    [DllImport("user32.dll")]
    private static extern IntPtr CopyImage(IntPtr image, uint type, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool SetSystemCursor(IntPtr cursor, uint id);

    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfoW(uint action, uint param, IntPtr value, uint winIni);

    private readonly HookProc hookProc;
    private IntPtr hook;
    private bool pickReady;
    private PointStruct pickPoint;
    private bool crosshair;
    private static bool exitHooked;

    public ToolsColorPicker()
    {
        hookProc = OnHook;
    }

    public bool Active => hook != IntPtr.Zero;

    public bool Start()
    {
        if (hook != IntPtr.Zero) return true;
        pickReady = false;
        try
        {
            hook = SetWindowsHookExW(WhMouseLl, hookProc, GetModuleHandleW(null), 0);
        }
        catch
        {
            hook = IntPtr.Zero;
        }
        if (hook != IntPtr.Zero) ShowCrosshair();
        return hook != IntPtr.Zero;
    }

    public void Stop()
    {
        var current = hook;
        hook = IntPtr.Zero;
        pickReady = false;
        RestoreCursors();
        if (current == IntPtr.Zero) return;
        try
        {
            UnhookWindowsHookEx(current);
        }
        catch
        {
        }
    }

    // Swaps every system cursor for the crosshair while picking; SetSystemCursor takes
    // ownership of each copy, and SPI_SETCURSORS reloads the user's own scheme afterwards.
    private void ShowCrosshair()
    {
        try
        {
            var cross = LoadCursorW(IntPtr.Zero, new IntPtr(IdcCross));
            if (cross == IntPtr.Zero) return;
            foreach (var id in SystemCursors)
            {
                var copy = CopyImage(cross, ImageCursor, 0, 0, 0);
                if (copy != IntPtr.Zero && SetSystemCursor(copy, id)) crosshair = true;
            }
            if (crosshair && !exitHooked)
            {
                exitHooked = true;
                AppDomain.CurrentDomain.ProcessExit += (_, _) => ResetSystemCursors();
            }
        }
        catch
        {
        }
    }

    private void RestoreCursors()
    {
        if (!crosshair) return;
        crosshair = false;
        ResetSystemCursors();
    }

    public static void ResetSystemCursors()
    {
        try
        {
            SystemParametersInfoW(SpiSetCursors, 0, IntPtr.Zero, 0);
        }
        catch
        {
        }
    }

    public bool EscapePressed()
    {
        try
        {
            return (GetAsyncKeyState(VkEscape) & 0x8000) != 0;
        }
        catch
        {
            return false;
        }
    }

    public bool TryTakePick(out int x, out int y)
    {
        x = pickPoint.X;
        y = pickPoint.Y;
        return pickReady;
    }

    public bool ReadCursorColor(out byte red, out byte green, out byte blue)
    {
        red = green = blue = 0;
        if (!GetCursorPos(out var point)) return false;
        return ReadPixel(point.X, point.Y, out red, out green, out blue);
    }

    public bool ReadPixel(int x, int y, out byte red, out byte green, out byte blue)
    {
        red = green = blue = 0;
        var dc = GetDC(IntPtr.Zero);
        if (dc == IntPtr.Zero) return false;
        try
        {
            var value = GetPixel(dc, x, y);
            if (value == 0xFFFFFFFF) return false;
            red = (byte)(value & 0xFF);
            green = (byte)((value >> 8) & 0xFF);
            blue = (byte)((value >> 16) & 0xFF);
            return true;
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, dc);
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private IntPtr OnHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && hook != IntPtr.Zero)
            {
                var message = (int)wParam;
                if (message == WmLButtonDown) return (IntPtr)1;
                if (message == WmLButtonUp)
                {
                    var data = Marshal.PtrToStructure<MouseHookData>(lParam);
                    pickPoint = data.Point;
                    pickReady = true;
                    return (IntPtr)1;
                }
            }
        }
        catch
        {
        }
        return CallNextHookEx(hook, nCode, wParam, lParam);
    }
}
