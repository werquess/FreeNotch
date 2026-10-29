using System.Text.Json;
using Microsoft.Win32;

namespace FreeNotch;

public enum IslandTheme
{
    Smoke,
    Oled
}

public sealed class Settings
{
    public IslandTheme Theme { get; set; } = IslandTheme.Smoke;
    public bool ReplaceVolumeFlyout { get; set; } = true;
    public Dictionary<string, double[]> Positions { get; set; } = new();

    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FreeNotch");
    private static string FilePath => Path.Combine(Folder, "settings.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

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
