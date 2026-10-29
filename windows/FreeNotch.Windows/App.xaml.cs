using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace FreeNotch;

public partial class App : Application
{
    private Mutex? instance;
    private Settings settings = new();
    private IslandWindow? island;
    private TrayIcon? tray;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        instance = new Mutex(true, "FreeNotch.Windows.SingleInstance", out var created);
        if (!created)
        {
            Exit();
            return;
        }

        settings = Settings.Load();
        var media = new MediaService(DispatcherQueue.GetForCurrentThread());
        island = new IslandWindow(settings, media);

        tray = new TrayIcon("FreeNotch", Path.Combine(AppContext.BaseDirectory, "Assets", "FreeNotch.ico"))
        {
            Clicked = () => island.Toggle(),
            MenuBuilder = BuildMenu,
        };

        island.Start();
        await media.StartAsync();
    }

    private IReadOnlyList<TrayIcon.Item> BuildMenu()
    {
        var w = island!;
        return new List<TrayIcon.Item>
        {
            new(w.IsOpen ? Strings.L("Close") : Strings.L("Open"), w.Toggle),
            TrayIcon.Item.Separator,
            new(Strings.L("Theme"), Children: new List<TrayIcon.Item>
            {
                new(Strings.L("Smoke Glass"), () => w.SetTheme(IslandTheme.Smoke), settings.Theme == IslandTheme.Smoke),
                new(Strings.L("OLED Black"), () => w.SetTheme(IslandTheme.Oled), settings.Theme == IslandTheme.Oled),
            }),
            new(Strings.L("Replace the Windows volume pop-up"), () => w.SetReplaceVolume(!settings.ReplaceVolumeFlyout), settings.ReplaceVolumeFlyout),
            new(Strings.L("Launch at login"), () => Settings.LaunchAtLogin = !Settings.LaunchAtLogin, Settings.LaunchAtLogin),
            new(Strings.L("Reset position"), w.ResetPosition),
            TrayIcon.Item.Separator,
            new(Strings.L("Quit FreeNotch"), Quit),
        };
    }

    private void Quit()
    {
        island?.Shutdown();
        tray?.Dispose();
        instance?.ReleaseMutex();
        Exit();
    }
}
