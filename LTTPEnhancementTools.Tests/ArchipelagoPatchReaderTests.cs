using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LTTPEnhancementTools.Services;

namespace LTTPEnhancementTools.Tests;

public class ArchipelagoPatchReaderTests : IDisposable
{
    private readonly string _tempDir;

    public ArchipelagoPatchReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "APReaderTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string CreateAplttp(string name, object? jsonContent, bool includeDelta = true)
    {
        string path = Path.Combine(_tempDir, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);

        if (jsonContent is not null)
        {
            var entry = zip.CreateEntry("archipelago.json");
            using var stream = entry.Open();
            JsonSerializer.Serialize(stream, jsonContent);
        }

        if (includeDelta)
        {
            var delta = zip.CreateEntry("delta.bsdiff4");
            using var ds = delta.Open();
            ds.Write(new byte[] { 0x00 }); // dummy content
        }

        return path;
    }

    // ── ReadPatch: valid file ─────────────────────────────────────────

    [Fact]
    public void ReadPatch_ValidFile_ReturnsMetadata()
    {
        string path = CreateAplttp("test.aplttp", new
        {
            server = "archipelago.gg:12345",
            player = 3,
            player_name = "Tiken",
            game = "A Link to the Past",
            base_checksum = "abc123"
        });

        var (metadata, error) = ArchipelagoPatchReader.ReadPatch(path);

        Assert.Null(error);
        Assert.NotNull(metadata);
        Assert.Equal("archipelago.gg:12345", metadata.Server);
        Assert.Equal(3, metadata.Player);
        Assert.Equal("Tiken", metadata.PlayerName);
        Assert.Equal("A Link to the Past", metadata.Game);
        Assert.Equal("abc123", metadata.BaseChecksum);
    }

    [Fact]
    public void ReadPatch_ExpectedSfcPath_SameDirAndStem()
    {
        string path = CreateAplttp("MyPatch.aplttp", new { server = "", player = 1, player_name = "", game = "" });

        var (metadata, error) = ArchipelagoPatchReader.ReadPatch(path);

        Assert.Null(error);
        Assert.NotNull(metadata);
        Assert.Equal(Path.Combine(_tempDir, "MyPatch.sfc"), metadata.ExpectedSfcPath);
        Assert.Equal(path, metadata.PatchFilePath);
    }

    [Fact]
    public void ReadPatch_MissingFields_DefaultsToEmptyAndZero()
    {
        // JSON with no fields at all
        string path = CreateAplttp("minimal.aplttp", new { });

        var (metadata, error) = ArchipelagoPatchReader.ReadPatch(path);

        Assert.Null(error);
        Assert.NotNull(metadata);
        Assert.Equal(string.Empty, metadata.Server);
        Assert.Equal(0, metadata.Player);
        Assert.Equal(string.Empty, metadata.PlayerName);
        Assert.Equal(string.Empty, metadata.Game);
        Assert.Equal(string.Empty, metadata.BaseChecksum);
    }

    // ── ReadPatch: error paths ────────────────────────────────────────

    [Fact]
    public void ReadPatch_NonexistentFile_ReturnsError()
    {
        var (metadata, error) = ArchipelagoPatchReader.ReadPatch(Path.Combine(_tempDir, "nope.aplttp"));

        Assert.Null(metadata);
        Assert.NotNull(error);
        Assert.Contains("not found", error);
    }

    [Fact]
    public void ReadPatch_NotAZip_ReturnsError()
    {
        string path = Path.Combine(_tempDir, "garbage.aplttp");
        File.WriteAllText(path, "this is not a zip file");

        var (metadata, error) = ArchipelagoPatchReader.ReadPatch(path);

        Assert.Null(metadata);
        Assert.NotNull(error);
        Assert.Contains("not a valid .aplttp archive", error);
    }

    [Fact]
    public void ReadPatch_MissingArchipelagoJson_ReturnsError()
    {
        // Create ZIP without archipelago.json
        string path = Path.Combine(_tempDir, "nojson.aplttp");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("delta.bsdiff4");
            using var s = entry.Open();
            s.Write(new byte[] { 0x00 });
        }

        var (metadata, error) = ArchipelagoPatchReader.ReadPatch(path);

        Assert.Null(metadata);
        Assert.NotNull(error);
        Assert.Contains("missing archipelago.json", error);
    }

    // ── ApplyPatch: error paths ───────────────────────────────────────

    [Fact]
    public void ApplyPatch_MissingBaseRom_ReturnsError()
    {
        string aplttp = CreateAplttp("test.aplttp", new { server = "", player = 1, player_name = "", game = "" });
        string missingRom = Path.Combine(_tempDir, "norom.sfc");

        var (sfcPath, error) = ArchipelagoPatchReader.ApplyPatch(aplttp, missingRom);

        Assert.Null(sfcPath);
        Assert.NotNull(error);
        Assert.Contains("Base ROM not found", error);
    }

    [Fact]
    public void ApplyPatch_ChecksumMismatch_ReturnsError()
    {
        // Create a dummy base ROM
        string romPath = Path.Combine(_tempDir, "base.sfc");
        File.WriteAllBytes(romPath, new byte[] { 0x01, 0x02, 0x03 });

        // Create .aplttp with a checksum that won't match
        string aplttp = CreateAplttp("test.aplttp", new
        {
            server = "",
            player = 1,
            player_name = "",
            game = "",
            base_checksum = "0000000000000000000000000000dead"
        });

        var (sfcPath, error) = ArchipelagoPatchReader.ApplyPatch(aplttp, romPath);

        Assert.Null(sfcPath);
        Assert.NotNull(error);
        Assert.Contains("checksum mismatch", error);
    }

    [Fact]
    public void ApplyPatch_MissingDelta_ReturnsError()
    {
        // Create a dummy base ROM and compute its MD5
        string romPath = Path.Combine(_tempDir, "base.sfc");
        byte[] romData = new byte[] { 0x01, 0x02, 0x03 };
        File.WriteAllBytes(romPath, romData);

        using var md5Stream = new MemoryStream(romData);
        string checksum = Convert.ToHexString(MD5.HashData(md5Stream)).ToLowerInvariant();

        // Create .aplttp with matching checksum but NO delta.bsdiff4
        string aplttp = CreateAplttp("test.aplttp", new
        {
            server = "",
            player = 1,
            player_name = "",
            game = "",
            base_checksum = checksum
        }, includeDelta: false);

        var (sfcPath, error) = ArchipelagoPatchReader.ApplyPatch(aplttp, romPath);

        Assert.Null(sfcPath);
        Assert.NotNull(error);
        Assert.Contains("missing delta.bsdiff4", error);
    }
}
