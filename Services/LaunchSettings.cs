using System.Text.Json.Serialization;

namespace LTTPEnhancementTools.Services;

public class LaunchSettings
{
    [JsonPropertyName("emulatorPath")]
    public string EmulatorPath { get; set; } = string.Empty;

    [JsonPropertyName("connectorScriptPath")]
    public string ConnectorScriptPath { get; set; } = string.Empty;

    [JsonPropertyName("sniPath")]
    public string SniPath { get; set; } = string.Empty;

    [JsonPropertyName("archipelagoLauncherPath")]
    public string ArchipelagoLauncherPath { get; set; } = string.Empty;

    [JsonPropertyName("trackerUrl")]
    public string TrackerUrl { get; set; } = string.Empty;

    [JsonPropertyName("seedUrl")]
    public string SeedUrl { get; set; } = string.Empty;

    [JsonPropertyName("baseRomPath")]
    public string BaseRomPath { get; set; } = string.Empty;
}
