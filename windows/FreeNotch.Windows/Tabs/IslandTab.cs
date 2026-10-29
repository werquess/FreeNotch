using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace FreeNotch;

public sealed class TabContext
{
    public TabContext(Settings settings, MediaService media, DispatcherQueue queue, IntPtr hwnd)
    {
        Settings = settings;
        Media = media;
        Queue = queue;
        Hwnd = hwnd;
    }

    public Settings Settings { get; }
    public MediaService Media { get; }
    public DispatcherQueue Queue { get; }
    public IntPtr Hwnd { get; }

    public static string DataFolder
    {
        get
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FreeNotch");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public Action<bool>? HoldOpen { get; set; }
    public Action? RequestClose { get; set; }
    public Action<string>? RequestTab { get; set; }
    public Action<string>? OpenTab { get; set; }
}

public abstract class IslandTab : Grid
{
    protected IslandTab(TabContext context)
    {
        Context = context;
    }

    protected TabContext Context { get; }

    public abstract string Id { get; }
    public abstract string Glyph { get; }
    public abstract string Title { get; }
    public virtual double ContentHeight => 150;

    public virtual void OnShown()
    {
    }

    public virtual void OnHidden()
    {
    }
}
