using System.Text.Json;
using Microsoft.Win32;

namespace FreeNotch;

public enum IslandTheme
{
    Smoke,
    Oled
}

public enum OpenMode
{
    Hover,
    Click
}

public enum EdgePosition
{
    Left,
    Center,
    Right
}

public sealed class Settings
{
    public IslandTheme Theme { get; set; } = IslandTheme.Smoke;
    public bool ReplaceVolumeFlyout { get; set; } = true;
    public OpenMode OpenMode { get; set; } = OpenMode.Hover;
    public int HoverDelay { get; set; } = 350;
    public int NotchWidth { get; set; } = 190;
    public int NotchHeight { get; set; } = 32;
    public bool ActivityMedia { get; set; } = true;
    public bool ActivityTimers { get; set; } = true;
    public EdgePosition Edge { get; set; } = EdgePosition.Center;

    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FreeNotch");
    private static string FilePath => Path.Combine(Folder, "settings.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Options) ?? new Settings();
            }
        }
        catch (Exception)
        {
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception)
        {
        }
    }

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool LaunchAtLogin
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue("FreeNotch") is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value)
            {
                key.SetValue("FreeNotch", $"\"{Environment.ProcessPath}\"");
            }
            else
            {
                key.DeleteValue("FreeNotch", false);
            }
        }
    }
}
