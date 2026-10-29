using NAudio.CoreAudioApi;

namespace FreeNotch;

public sealed class VolumeService
{
    private const float Step = 0.02f;
    private readonly MMDeviceEnumerator enumerator = new();

    public (float Level, bool Muted) Change(int direction)
    {
        try
        {
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var volume = device.AudioEndpointVolume;
            if (direction == 0)
            {
                volume.Mute = !volume.Mute;
            }
            else
            {
                if (volume.Mute && direction > 0) volume.Mute = false;
                var target = MathF.Round((volume.MasterVolumeLevelScalar + direction * Step) / Step) * Step;
                volume.MasterVolumeLevelScalar = Math.Clamp(target, 0f, 1f);
            }
            return (volume.MasterVolumeLevelScalar, volume.Mute);
        }
        catch (Exception)
        {
            return (0, false);
        }
    }
}

public sealed class MediaKeyHook : IDisposable
{
    private readonly Native.HookProc proc;
    private IntPtr hook;

    public Func<uint, bool, bool>? Handler { get; set; }

    public MediaKeyHook()
    {
        proc = Callback;
    }

    public void Start()
    {
        if (hook != IntPtr.Zero) return;
        hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, proc, Native.GetModuleHandle(null), 0);
    }

    public void Stop()
    {
        if (hook == IntPtr.Zero) return;
        Native.UnhookWindowsHookEx(hook);
        hook = IntPtr.Zero;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && Handler != null)
        {
            var data = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
            var vk = data.vkCode;
            if (vk is Native.VK_VOLUME_UP or Native.VK_VOLUME_DOWN or Native.VK_VOLUME_MUTE)
            {
                var message = (int)wParam;
                var down = message is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
                if (Handler(vk, down)) return new IntPtr(1);
            }
        }
        return Native.CallNextHookEx(hook, nCode, wParam, lParam);
    }

    public void Dispose() => Stop();
}
