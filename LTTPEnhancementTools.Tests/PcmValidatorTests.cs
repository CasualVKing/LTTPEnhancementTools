using System.Text;
using LTTPEnhancementTools.Services;

namespace LTTPEnhancementTools.Tests;

public class PcmValidatorTests : IDisposable
{
    private readonly string _tempDir;

    public PcmValidatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PcmValidatorTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string WriteTempFile(string name, byte[] content)
    {
        string path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    // ── Valid file ──────────────────────────────────────────────────────

    [Fact]
    public void Valid_PcmFile_ReturnsNull()
    {
        // MSU1 header (4 bytes) + loop point 0 (4 bytes) + some audio data
        var data = new byte[16];
        Encoding.ASCII.GetBytes("MSU1").CopyTo(data, 0);
        // loop point = 0 (already zeroed)
        // audio bytes (8 bytes of silence)

        string path = WriteTempFile("valid.pcm", data);

        Assert.Null(PcmValidator.Validate(path));
    }

    [Fact]
    public void Valid_PcmFile_WithLoopPoint_ReturnsNull()
    {
        var data = new byte[16];
        Encoding.ASCII.GetBytes("MSU1").CopyTo(data, 0);
        BitConverter.GetBytes((uint)44100).CopyTo(data, 4); // loop at 1 second
        // 8 bytes audio data

        string path = WriteTempFile("looped.pcm", data);

        Assert.Null(PcmValidator.Validate(path));
    }

    // ── Invalid files ───────────────────────────────────────────────────

    [Fact]
    public void NonexistentFile_ReturnsError()
    {
        string path = Path.Combine(_tempDir, "doesnotexist.pcm");

        string? error = PcmValidator.Validate(path);

        Assert.NotNull(error);
        Assert.Contains("does not exist", error);
    }

    [Fact]
    public void FileTooSmall_ReturnsError()
    {
        string path = WriteTempFile("tiny.pcm", new byte[4]); // only 4 bytes

        string? error = PcmValidator.Validate(path);

        Assert.NotNull(error);
        Assert.Contains("too small", error);
    }

    [Fact]
    public void WrongSignature_ReturnsError()
    {
        var data = new byte[16];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(data, 0); // WAV header, not MSU1

        string path = WriteTempFile("wrong_sig.pcm", data);

        string? error = PcmValidator.Validate(path);

        Assert.NotNull(error);
        Assert.Contains("Invalid MSU-1 signature", error);
    }

    [Fact]
    public void HeaderOnly_NoAudioData_ReturnsError()
    {
        var data = new byte[8]; // exactly header size, no audio
        Encoding.ASCII.GetBytes("MSU1").CopyTo(data, 0);

        string path = WriteTempFile("header_only.pcm", data);

        string? error = PcmValidator.Validate(path);

        Assert.NotNull(error);
        Assert.Contains("no audio data", error);
    }

    [Fact]
    public void EmptyFile_ReturnsError()
    {
        string path = WriteTempFile("empty.pcm", Array.Empty<byte>());

        string? error = PcmValidator.Validate(path);

        Assert.NotNull(error);
        Assert.Contains("too small", error);
    }
}
