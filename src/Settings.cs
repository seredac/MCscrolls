using System.Text.Json;
using Microsoft.Win32;

namespace MCscrolls;

internal sealed class Settings
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MCscrolls");
    private static readonly string SettingsPath = Path.Combine(AppDataDir, "settings.json");
    private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "MCscrolls";

    public bool Enabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public int CooldownMs { get; set; } = 150;
    public bool GhostCursorsEnabled { get; set; } = true;

    public static Settings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
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
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
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
