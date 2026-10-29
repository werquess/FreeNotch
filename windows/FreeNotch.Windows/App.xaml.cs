using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace FreeNotch;

public partial class App : Application
{
    private Mutex? instance;
    private Settings settings = new();
    private IslandWindow? island;
    private TrayIcon? tray;
    private SettingsWindow? settingsWindow;

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
            new(Strings.L("Settings…"), ShowSettings),
            TrayIcon.Item.Separator,
            new(Strings.L("Quit FreeNotch"), Quit),
        };
    }

    private void ShowSettings()
    {
        if (settingsWindow == null)
        {
            settingsWindow = new SettingsWindow(settings, island!);
            settingsWindow.Closed += (_, _) => settingsWindow = null;
        }
        settingsWindow.Activate();
    }

    private void Quit()
    {
        island?.Shutdown();
        tray?.Dispose();
        instance?.ReleaseMutex();
        Exit();
    }
}
