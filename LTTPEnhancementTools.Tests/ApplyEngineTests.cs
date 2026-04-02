using System.Text;
using LTTPEnhancementTools.Services;

namespace LTTPEnhancementTools.Tests;

public class ApplyEngineTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sourceDir;
    private readonly string _outputDir;
    private readonly ApplyEngine _engine;
    private readonly Progress<(string step, int current, int total)> _progress;

    public ApplyEngineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ApplyEngineTests_" + Guid.NewGuid().ToString("N")[..8]);
        _sourceDir = Path.Combine(_tempDir, "source");
        _outputDir = Path.Combine(_tempDir, "output");
        Directory.CreateDirectory(_sourceDir);
        Directory.CreateDirectory(_outputDir);
        _engine = new ApplyEngine();
        _progress = new Progress<(string step, int current, int total)>();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string CreateDummyRom(string name = "test.sfc")
    {
        // Create a ROM large enough for sprite injection (>0xDEDF9 bytes)
        string path = Path.Combine(_sourceDir, name);
        var data = new byte[0xDF000]; // ~900KB, enough for all sprite offsets
        File.WriteAllBytes(path, data);
        return path;
    }

    private string CreateDummyPcm(string name)
    {
        string path = Path.Combine(_sourceDir, name);
        var data = new byte[16];
        Encoding.ASCII.GetBytes("MSU1").CopyTo(data, 0);
        File.WriteAllBytes(path, data);
        return path;
    }

    // ── InPlace mode ──────────────────────────────────────────────────

    [Fact]
    public async Task InPlace_False_CopiesRomToOutputDir()
    {
        string rom = CreateDummyRom();
        var req = new ApplyRequest(rom, _outputDir, new Dictionary<string, string>(),
            OverwriteMode.Overwrite, SpriteSourcePath: null, InPlace: false);

        // Need at least one track or sprite — but CanApply is UI-level check.
        // Engine itself runs with empty tracks. Let's add a track.
        string pcm = CreateDummyPcm("track.pcm");
        var tracks = new Dictionary<string, string> { ["2"] = pcm };
        req = req with { Tracks = tracks };

        var result = await _engine.RunAsync(req, _progress);

        string expectedRomCopy = Path.Combine(_outputDir, "test.sfc");
        Assert.True(File.Exists(expectedRomCopy), "ROM should be copied to output dir");
        Assert.Contains(expectedRomCopy, result.FilesWritten);
        // Source ROM should still exist
        Assert.True(File.Exists(rom));
    }

    [Fact]
    public async Task InPlace_True_DoesNotCopyRom()
    {
        string rom = CreateDummyRom();
        byte[] originalContent = File.ReadAllBytes(rom);
        string pcm = CreateDummyPcm("track.pcm");
        var tracks = new Dictionary<string, string> { ["2"] = pcm };

        var req = new ApplyRequest(rom, _sourceDir, tracks,
            OverwriteMode.Overwrite, InPlace: true);

        var result = await _engine.RunAsync(req, _progress);

        // ROM should NOT appear in filesWritten (it wasn't copied)
        Assert.DoesNotContain(result.FilesWritten, f => f.EndsWith(".sfc"));
        // PCM should be written next to the source ROM
        string expectedPcm = Path.Combine(_sourceDir, "test-2.pcm");
        Assert.True(File.Exists(expectedPcm));
        Assert.Contains(expectedPcm, result.FilesWritten);
    }

    [Fact]
    public async Task InPlace_True_NoSprite_RomUnchanged()
    {
        string rom = CreateDummyRom();
        byte[] originalContent = File.ReadAllBytes(rom);
        string pcm = CreateDummyPcm("track.pcm");
        var tracks = new Dictionary<string, string> { ["5"] = pcm };

        var req = new ApplyRequest(rom, _sourceDir, tracks,
            OverwriteMode.Overwrite, SpriteSourcePath: null, InPlace: true);

        await _engine.RunAsync(req, _progress);

        // ROM content should be unchanged (no sprite applied)
        byte[] afterContent = File.ReadAllBytes(rom);
        Assert.Equal(originalContent, afterContent);
    }

    // ── .msu marker ───────────────────────────────────────────────────

    [Fact]
    public async Task TracksAssigned_MsuFileCreated()
    {
        string rom = CreateDummyRom();
        string pcm = CreateDummyPcm("track.pcm");
        var tracks = new Dictionary<string, string> { ["2"] = pcm };

        var req = new ApplyRequest(rom, _outputDir, tracks,
            OverwriteMode.Overwrite, InPlace: false);

        var result = await _engine.RunAsync(req, _progress);

        string msuPath = Path.Combine(_outputDir, "test.msu");
        Assert.True(File.Exists(msuPath), ".msu marker should exist when tracks are assigned");
        Assert.Contains(msuPath, result.FilesWritten);
        Assert.Equal(0, new FileInfo(msuPath).Length); // 0-byte marker
    }

    [Fact]
    public async Task NoTracks_MsuFileNotCreated()
    {
        string rom = CreateDummyRom();
        // Empty tracks — this simulates sprite-only apply
        // We need to test the engine directly; CanApply UI check is separate
        var tracks = new Dictionary<string, string>();

        // Create a minimal valid .zspr sprite so the engine has something to do
        string spritePath = Path.Combine(_sourceDir, "test.spr");
        File.WriteAllBytes(spritePath, new byte[0x7000]); // minimal .spr

        var req = new ApplyRequest(rom, _outputDir, tracks,
            OverwriteMode.Overwrite, SpriteSourcePath: spritePath, InPlace: false);

        var result = await _engine.RunAsync(req, _progress);

        string msuPath = Path.Combine(_outputDir, "test.msu");
        Assert.False(File.Exists(msuPath), ".msu marker should NOT exist when no tracks assigned");
        Assert.DoesNotContain(result.FilesWritten, f => f.EndsWith(".msu"));
    }

    // ── PCM copying ───────────────────────────────────────────────────

    [Fact]
    public async Task MultipleTracks_AllPcmsCopiedWithCorrectNames()
    {
        string rom = CreateDummyRom();
        string pcm1 = CreateDummyPcm("light_world.pcm");
        string pcm2 = CreateDummyPcm("dark_world.pcm");
        var tracks = new Dictionary<string, string>
        {
            ["2"] = pcm1,
            ["15"] = pcm2
        };

        var req = new ApplyRequest(rom, _outputDir, tracks,
            OverwriteMode.Overwrite, InPlace: false);

        var result = await _engine.RunAsync(req, _progress);

        Assert.True(File.Exists(Path.Combine(_outputDir, "test-2.pcm")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "test-15.pcm")));
        // ROM + .msu + 2 PCMs = 4 files
        Assert.Equal(4, result.FilesWritten.Count);
    }

    [Fact]
    public async Task MissingRom_ThrowsFileNotFound()
    {
        string missingRom = Path.Combine(_sourceDir, "nope.sfc");
        var req = new ApplyRequest(missingRom, _outputDir,
            new Dictionary<string, string>(), OverwriteMode.Overwrite);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _engine.RunAsync(req, _progress));
    }

    [Fact]
    public async Task CustomOutputBaseName_UsedForAllFiles()
    {
        string rom = CreateDummyRom();
        string pcm = CreateDummyPcm("track.pcm");
        var tracks = new Dictionary<string, string> { ["3"] = pcm };

        var req = new ApplyRequest(rom, _outputDir, tracks,
            OverwriteMode.Overwrite, OutputBaseName: "MyCustomPack", InPlace: false);

        var result = await _engine.RunAsync(req, _progress);

        Assert.True(File.Exists(Path.Combine(_outputDir, "MyCustomPack.sfc")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "MyCustomPack.msu")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "MyCustomPack-3.pcm")));
    }
}
