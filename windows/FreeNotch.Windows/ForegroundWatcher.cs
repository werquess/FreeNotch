using System.Diagnostics;

namespace FreeNotch;

public sealed class ForegroundWatcher : IDisposable
{
    private readonly Native.WinEventProc proc;
    private IntPtr hook;

    public event Action? Changed;

    public string ProcessName { get; private set; } = "";

    public ForegroundWatcher()
    {
        proc = (_, _, hwnd, _, _, _, _) => Update(hwnd);
        hook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, proc, 0, 0, Native.WINEVENT_OUTOFCONTEXT);
        Update(Native.GetForegroundWindow());
    }

    private void Update(IntPtr hwnd)
    {
        var name = "";
        try
        {
            Native.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid != 0)
            {
                using var process = Process.GetProcessById((int)pid);
                name = process.ProcessName;
            }
        }
        catch (Exception)
        {
        }
        if (string.Equals(name, ProcessName, StringComparison.OrdinalIgnoreCase)) return;
        ProcessName = name;
        Changed?.Invoke();
    }

    public bool Matches(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(ProcessName)) return false;
        var id = sourceId.ToLowerInvariant();
        var name = ProcessName.ToLowerInvariant();
        if (id.Contains(name)) return true;
        return name switch
        {
            "msedge" => id.Contains("edge"),
            "chrome" => id.Contains("chrome"),
            "spotify" => id.Contains("spotify"),
            "firefox" => id.Contains("firefox") || id.Contains("308046b0af4a39cb"),
            "brave" => id.Contains("brave"),
            "opera" => id.Contains("opera"),
            "applemusic" or "music" => id.Contains("applemusic") || id.Contains("apple"),
            _ => false,
        };
    }

    public void Dispose()
    {
        if (hook == IntPtr.Zero) return;
        Native.UnhookWinEvent(hook);
        hook = IntPtr.Zero;
    }
}
