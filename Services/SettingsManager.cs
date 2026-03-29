using System.IO;
using System.Text.Json;

namespace LTTPEnhancementTools.Services;

public static class SettingsManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LTTPEnhancementTools", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            string json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonDefaults.Standard) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonDefaults.Standard));
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SettingsManager] Save failed: {ex.Message}"); }
    }
}
