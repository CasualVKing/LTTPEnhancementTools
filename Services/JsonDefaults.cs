using System.Net.Http;
using System.Text.Json;

namespace LTTPEnhancementTools.Services;

/// <summary>Shared JSON serializer options to avoid duplicate allocations.</summary>
public static class JsonDefaults
{
    /// <summary>Indented output, case-insensitive property matching.</summary>
    public static readonly JsonSerializerOptions Standard = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Case-insensitive property matching only (no indentation).</summary>
    public static readonly JsonSerializerOptions ReadOnly = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>Shared HttpClient instance for API calls (30-second timeout).</summary>
public static class SharedHttp
{
    public static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };
}
