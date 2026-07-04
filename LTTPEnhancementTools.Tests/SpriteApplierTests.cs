using LTTPEnhancementTools.Services;

namespace LTTPEnhancementTools.Tests;

public class SpriteApplierTests : IDisposable
{
    private const int RomGfxOffset     = 0x80000;
    private const int RomPaletteOffset = 0xDD308;
    private const int RomGlovesOffset  = 0xDEDF5;
    private const int RomGfxMaxLength  = 0x7000;

    private readonly string _tempDir;

    public SpriteApplierTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SpriteApplierTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string CreateRom(byte fillByte = 0xAA)
    {
        string path = Path.Combine(_tempDir, "test.sfc");
        var data = new byte[0xDF000];
        Array.Fill(data, fillByte);
        File.WriteAllBytes(path, data);
        return path;
    }

    /// <summary>Builds a minimal valid ZSPR: 21-byte header + gfx + 124-byte palette.</summary>
    private string CreateZspr(string name, int gfxLength, byte gfxFill)
    {
        const int headerSize = 21;
        const int palLength = 124; // 120 palette + 4 gloves

        var data = new byte[headerSize + gfxLength + palLength];
        data[0] = 0x5A; data[1] = 0x53; data[2] = 0x50; data[3] = 0x52; // "ZSPR"
        BitConverter.GetBytes((uint)headerSize).CopyTo(data, 9);                  // gfx offset
        BitConverter.GetBytes((ushort)gfxLength).CopyTo(data, 13);                // gfx length
        BitConverter.GetBytes((uint)(headerSize + gfxLength)).CopyTo(data, 15);   // palette offset
        BitConverter.GetBytes((ushort)palLength).CopyTo(data, 19);                // palette length

        for (int i = 0; i < gfxLength; i++)
            data[headerSize + i] = gfxFill;

        string path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, data);
        return path;
    }

    [Fact]
    public void Apply_ValidZspr_WritesGfxToRom()
    {
        string rom = CreateRom();
        string zspr = CreateZspr("a.zspr", RomGfxMaxLength, 0x11);

        string? error = SpriteApplier.Apply(zspr, rom);

        Assert.Null(error);
        var patched = File.ReadAllBytes(rom);
        Assert.Equal(0x11, patched[RomGfxOffset]);
        Assert.Equal(0x11, patched[RomGfxOffset + RomGfxMaxLength - 1]);
    }

    [Fact]
    public void Apply_PreserveOriginal_CreatesBackupSidecar()
    {
        string rom = CreateRom();
        string zspr = CreateZspr("a.zspr", RomGfxMaxLength, 0x11);

        string? error = SpriteApplier.Apply(zspr, rom, preserveOriginal: true);

        Assert.Null(error);
        Assert.True(File.Exists(rom + SpriteApplier.BackupExtension));
    }

    [Fact]
    public void Apply_PreserveOriginal_ShorterSecondSprite_NoResidueFromFirst()
    {
        string rom = CreateRom(fillByte: 0xAA);
        string bigSprite   = CreateZspr("big.zspr", RomGfxMaxLength, 0x11);
        string smallSprite = CreateZspr("small.zspr", 0x1000, 0x22);

        Assert.Null(SpriteApplier.Apply(bigSprite, rom, preserveOriginal: true));
        Assert.Null(SpriteApplier.Apply(smallSprite, rom, preserveOriginal: true));

        var patched = File.ReadAllBytes(rom);
        // Small sprite's own data
        Assert.Equal(0x22, patched[RomGfxOffset]);
        Assert.Equal(0x22, patched[RomGfxOffset + 0x0FFF]);
        // Beyond the small sprite: original ROM bytes, NOT the big sprite's 0x11
        Assert.Equal(0xAA, patched[RomGfxOffset + 0x1000]);
        Assert.Equal(0xAA, patched[RomGfxOffset + RomGfxMaxLength - 1]);
    }

    [Fact]
    public void Apply_WithoutPreserveOriginal_NoSidecarCreated()
    {
        string rom = CreateRom();
        string zspr = CreateZspr("a.zspr", RomGfxMaxLength, 0x11);

        Assert.Null(SpriteApplier.Apply(zspr, rom));

        Assert.False(File.Exists(rom + SpriteApplier.BackupExtension));
    }

    [Fact]
    public void Apply_PreserveOriginal_GlovesAndPaletteRestored()
    {
        string rom = CreateRom(fillByte: 0xAA);
        string spriteA = CreateZspr("a.zspr", RomGfxMaxLength, 0x11);

        Assert.Null(SpriteApplier.Apply(spriteA, rom, preserveOriginal: true));

        // The backup should hold the ORIGINAL (0xAA) palette/gloves regions
        var backup = File.ReadAllBytes(rom + SpriteApplier.BackupExtension);
        Assert.Equal(RomGfxMaxLength + 120 + 4, backup.Length);
        Assert.All(backup, b => Assert.Equal(0xAA, b));
    }
}
