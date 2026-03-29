using System.IO;
using System.Text.Json;

namespace LTTPEnhancementTools.Services;

public static class AutoSaveManager
{
    private static readonly string AutoSavePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LTTPEnhancementTools", "autoSave.json");

    public static AutoSaveState Load()
    {
        try
        {
            if (!File.Exists(AutoSavePath)) return new AutoSaveState();
            string json = File.ReadAllText(AutoSavePath);
            return JsonSerializer.Deserialize<AutoSaveState>(json, JsonDefaults.Standard) ?? new AutoSaveState();
        }
        catch
        {
            return new AutoSaveState();
        }
    }

    public static void Save(AutoSaveState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AutoSavePath)!);
            File.WriteAllText(AutoSavePath, JsonSerializer.Serialize(state, JsonDefaults.Standard));
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AutoSaveManager] Save failed: {ex.Message}"); }
    }
}
