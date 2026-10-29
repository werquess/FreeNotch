using System.Runtime.InteropServices;

namespace FreeNotch;

public sealed class MouseHook : IDisposable
{
    private readonly Native.HookProc proc;
    private IntPtr hook;

    public Action<int, int>? Moved { get; set; }

    public MouseHook()
    {
        proc = Callback;
    }

    public void Start()
    {
        if (hook != IntPtr.Zero) return;
        hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, proc, Native.GetModuleHandle(null), 0);
    }

    public void Stop()
    {
        if (hook == IntPtr.Zero) return;
        Native.UnhookWindowsHookEx(hook);
        hook = IntPtr.Zero;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (int)wParam == Native.WM_MOUSEMOVE && Moved != null)
        {
            var data = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
            Moved(data.pt.X, data.pt.Y);
        }
        return Native.CallNextHookEx(hook, nCode, wParam, lParam);
    }

    public void Dispose() => Stop();
}
