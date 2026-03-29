using System.IO;
using System.Text.Json;
using LTTPEnhancementTools.Models;

namespace LTTPEnhancementTools.Services;

public static class PlaylistManager
{
    public static (Playlist? playlist, string? error) Load(string filePath)
    {
        try
        {
            string json = File.ReadAllText(filePath);
            var playlist = JsonSerializer.Deserialize<Playlist>(json, JsonDefaults.Standard);
            if (playlist is null)
                return (null, "Playlist file is empty or malformed.");
            if (playlist.Version != 1)
                return (null, $"Unsupported playlist version: {playlist.Version}");
            return (playlist, null);
        }
        catch (Exception ex)
        {
            return (null, $"Failed to load playlist: {ex.Message}");
        }
    }

    public static string? Save(string filePath, Playlist playlist)
    {
        try
        {
            playlist.LastModified = DateTime.UtcNow.ToString("o");
            var normalizedTracks = playlist.Tracks
                .ToDictionary(kv => kv.Key, kv => kv.Value.Replace('\\', '/'));
            playlist.Tracks = normalizedTracks;

            string json = JsonSerializer.Serialize(playlist, JsonDefaults.Standard);
            File.WriteAllText(filePath, json);
            return null;
        }
        catch (Exception ex)
        {
            return $"Failed to save playlist: {ex.Message}";
        }
    }
}
