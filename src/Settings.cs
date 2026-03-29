using System.Text.Json;
using Microsoft.Win32;

namespace MCscrolls;

public class HotkeySettings
{
    public List<string> WithinSetModifier { get; set; } = new() { "Alt" };
    public List<string> BetweenSetModifier { get; set; } = new() { "Ctrl", "Shift" };
}

public class GhostStyleSettings
{
    public int Opacity { get; set; } = 100;
    public string Color { get; set; } = "#FFFFFF";
    public int Size { get; set; } = 26;
    public Dictionary<string, string> PerSetColors { get; set; } = new();
}

public class MonitorSetConfig
{
    public string Name { get; set; } = "";
    public List<MonitorEntry> Monitors { get; set; } = new();
}

public class MonitorEntry
{
    public string DeviceName { get; set; } = "";
    public int Order { get; set; }
    public bool BoxedOut { get; set; }
}

internal sealed class Settings
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MCscrolls");
    private static readonly string SettingsPath = Path.Combine(AppDataDir, "settings.json");
    private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "MCscrolls";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    // V1 properties (preserved exactly)
    public bool Enabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public int CooldownMs { get; set; } = 150;
    public bool GhostCursorsEnabled { get; set; } = true;

    // V2 properties
    public int BetweenSetCooldownMs { get; set; } = 300;
    public HotkeySettings Hotkeys { get; set; } = new();
    public GhostStyleSettings GhostStyle { get; set; } = new();
    public List<MonitorSetConfig> Sets { get; set; } = new();
    public string ActiveSetName { get; set; } = "All";

    public static Settings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<Settings>(json, SerializerOptions) ?? new Settings();
            }
        }
        catch
        {
            // Fall through to defaults
        }

        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            string json = JsonSerializer.Serialize(this, SerializerOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Best effort
        }
    }

    public void SetStartup(bool enable)
    {
        StartWithWindows = enable;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, writable: true);
            if (key == null) return;

            if (enable)
            {
                string? exePath = Environment.ProcessPath;
                if (exePath != null)
                    key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Best effort
        }

        Save();
    }

    public bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }
}
