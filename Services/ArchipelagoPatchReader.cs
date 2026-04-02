using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using BsDiff;
using LTTPEnhancementTools.Models;

namespace LTTPEnhancementTools.Services;

public static class ArchipelagoPatchReader
{
    public static (ArchipelagoMetadata? metadata, string? error) ReadPatch(string aplttpPath)
    {
        if (!File.Exists(aplttpPath))
            return (null, $"Patch file not found: {aplttpPath}");

        try
        {
            using var zip = ZipFile.OpenRead(aplttpPath);
            var entry = zip.GetEntry("archipelago.json");
            if (entry is null)
                return (null, "Invalid .aplttp file: missing archipelago.json");

            using var stream = entry.Open();
            var json = JsonSerializer.Deserialize<ArchipelagoJson>(stream, JsonDefaults.ReadOnly);
            if (json is null)
                return (null, "Failed to parse archipelago.json");

            string dir = Path.GetDirectoryName(aplttpPath)!;
            string stem = Path.GetFileNameWithoutExtension(aplttpPath);
            string sfcPath = Path.Combine(dir, stem + ".sfc");

            var metadata = new ArchipelagoMetadata(
                Server: json.Server ?? string.Empty,
                Player: json.Player,
                PlayerName: json.PlayerName ?? string.Empty,
                Game: json.Game ?? string.Empty,
                PatchFilePath: aplttpPath,
                ExpectedSfcPath: sfcPath,
                BaseChecksum: json.BaseChecksum ?? string.Empty
            );

            return (metadata, null);
        }
        catch (InvalidDataException)
        {
            return (null, "File is not a valid .aplttp archive.");
        }
        catch (Exception ex)
        {
            return (null, $"Error reading patch: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies the bsdiff4 patch from the .aplttp to the base ROM, producing the .sfc output.
    /// </summary>
    public static (string? sfcPath, string? error) ApplyPatch(string aplttpPath, string baseRomPath)
    {
        if (!File.Exists(baseRomPath))
            return (null, $"Base ROM not found: {baseRomPath}");

        var (metadata, readError) = ReadPatch(aplttpPath);
        if (readError is not null)
            return (null, readError);

        // Validate base ROM checksum
        if (!string.IsNullOrEmpty(metadata!.BaseChecksum))
        {
            string actualHash = ComputeMd5(baseRomPath);
            if (!string.Equals(actualHash, metadata.BaseChecksum, StringComparison.OrdinalIgnoreCase))
                return (null, $"Base ROM checksum mismatch.\n\nExpected: {metadata.BaseChecksum}\nActual: {actualHash}\n\nMake sure you're using the correct vanilla ALttP ROM.");
        }

        try
        {
            string sfcPath = metadata.ExpectedSfcPath;

            // Extract delta.bsdiff4 into memory (ZIP entry streams aren't seekable)
            byte[] patchBytes;
            using (var zip = ZipFile.OpenRead(aplttpPath))
            {
                var deltaEntry = zip.GetEntry("delta.bsdiff4")
                    ?? throw new InvalidDataException("Invalid .aplttp file: missing delta.bsdiff4");
                using var entryStream = deltaEntry.Open();
                using var ms = new MemoryStream();
                entryStream.CopyTo(ms);
                patchBytes = ms.ToArray();
            }

            using var baseRomStream = new MemoryStream(File.ReadAllBytes(baseRomPath));
            using var outputStream = File.Create(sfcPath);

            // BinaryPatch.Apply may call openPatchStream multiple times;
            // return a fresh MemoryStream each time so disposal doesn't affect us
            BinaryPatch.Apply(baseRomStream, () => new MemoryStream(patchBytes), outputStream);

            return (sfcPath, null);
        }
        catch (InvalidDataException ex)
        {
            return (null, $"Patch application failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (null, $"Error applying patch: {ex.Message}");
        }
    }

    private static string ComputeMd5(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        byte[] hash = MD5.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private class ArchipelagoJson
    {
        [JsonPropertyName("server")]
        public string? Server { get; set; }

        [JsonPropertyName("player")]
        public int Player { get; set; }

        [JsonPropertyName("player_name")]
        public string? PlayerName { get; set; }

        [JsonPropertyName("game")]
        public string? Game { get; set; }

        [JsonPropertyName("base_checksum")]
        public string? BaseChecksum { get; set; }
    }
}
