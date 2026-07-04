using System;
using System.IO;

namespace LTTPEnhancementTools.Services;

/// <summary>
/// Validates and applies .zspr / .spr sprite files to a ROM copy.
/// ROM write addresses sourced from pyz3r reference implementation.
/// </summary>
public static class SpriteApplier
{
    // ROM addresses for sprite data
    private const int RomGfxOffset     = 0x80000;   // Sprite pixel data
    private const int RomPaletteOffset = 0xDD308;   // Palette data (120 bytes)
    private const int RomGlovesOffset  = 0xDEDF5;   // Gloves palette (4 bytes)
    private const int RomGfxMaxLength  = 0x7000;    // 28,672 bytes max
    private const int RomPaletteLength = 120;

    /// <summary>Sidecar extension for the original-region backup used by preserveOriginal.</summary>
    public const string BackupExtension = ".spritebak";

    // ZSPR header field byte offsets
    private const int ZsprGfxOffsetPos     = 9;
    private const int ZsprGfxLengthPos     = 13;
    private const int ZsprPaletteOffsetPos = 15;
    private const int ZsprPaletteLengthPos = 19;
    private const int ZsprMinHeaderSize    = 21;

    private static readonly byte[] ZsprMagic = { 0x5A, 0x53, 0x50, 0x52 }; // "ZSPR"

    /// <summary>
    /// Validates a sprite file. Returns null on success, or an error string.
    /// </summary>
    public static string? Validate(string path)
    {
        if (!File.Exists(path))
            return "Sprite file not found.";

        var ext = Path.GetExtension(path).ToLowerInvariant();

        try
        {
            if (ext == ".zspr")
            {
                using var fs = File.OpenRead(path);
                if (fs.Length < ZsprMinHeaderSize)
                    return "File is too small to be a valid .zspr.";

                var magic = new byte[4];
                fs.ReadExactly(magic, 0, 4);
                for (int i = 0; i < 4; i++)
                    if (magic[i] != ZsprMagic[i])
                        return "File does not have a valid ZSPR header (wrong magic bytes).";

                return null; // valid
            }
            else if (ext == ".spr")
            {
                var info = new FileInfo(path);
                if (info.Length < RomGfxMaxLength)
                    return $"Legacy .spr file must be at least 0x7000 ({RomGfxMaxLength}) bytes.";

                return null; // valid
            }
            else
            {
                return "Unsupported sprite format. Please use .zspr or .spr files.";
            }
        }
        catch (Exception ex)
        {
            return $"Error reading sprite file: {ex.Message}";
        }
    }

    /// <summary>
    /// Applies a sprite file to the ROM at romDestPath (in-place patch).
    /// When <paramref name="preserveOriginal"/> is true (repeated in-place applies to the
    /// same ROM), the ROM's original sprite regions are backed up to a .spritebak sidecar
    /// on first apply and restored before each subsequent apply, so a sprite with shorter
    /// pixel data never inherits residue from the previously applied sprite.
    /// Returns null on success, or an error string.
    /// </summary>
    public static string? Apply(string spritePath, string romDestPath, bool preserveOriginal = false)
    {
        try
        {
            var ext = Path.GetExtension(spritePath).ToLowerInvariant();
            var rom = File.ReadAllBytes(romDestPath);

            if (preserveOriginal)
                RestoreOrBackupOriginalRegions(rom, romDestPath);

            if (ext == ".zspr")
            {
                var sprite = File.ReadAllBytes(spritePath);

                if (sprite.Length < ZsprMinHeaderSize)
                    return "ZSPR file is too small to contain a valid header.";

                // Parse header fields (little-endian)
                uint gfxOffset     = BitConverter.ToUInt32(sprite, ZsprGfxOffsetPos);
                ushort gfxLength   = BitConverter.ToUInt16(sprite, ZsprGfxLengthPos);
                uint palOffset     = BitConverter.ToUInt32(sprite, ZsprPaletteOffsetPos);
                ushort palLength   = BitConverter.ToUInt16(sprite, ZsprPaletteLengthPos);

                // Validate bounds within sprite file
                if (gfxOffset + gfxLength > sprite.Length)
                    return "ZSPR pixel data region exceeds file size.";
                if (palOffset + palLength > sprite.Length)
                    return "ZSPR palette data region exceeds file size.";
                if (gfxLength > RomGfxMaxLength)
                    return $"ZSPR pixel data length ({gfxLength}) exceeds maximum ({RomGfxMaxLength}).";

                // Validate ROM bounds
                if (RomGfxOffset + gfxLength > rom.Length)
                    return "ROM file is too small to receive sprite pixel data.";
                if (palLength >= 4 && RomPaletteOffset + (palLength - 4) > rom.Length)
                    return "ROM file is too small to receive sprite palette data.";
                if (palLength >= 4 && RomGlovesOffset + 4 > rom.Length)
                    return "ROM file is too small to receive gloves palette data.";

                // Write pixel data
                Array.Copy(sprite, (int)gfxOffset, rom, RomGfxOffset, gfxLength);

                // Write palette data (last 4 bytes of palette are gloves)
                if (palLength >= 4)
                {
                    int mainPalLength = palLength - 4;
                    if (mainPalLength > 0)
                        Array.Copy(sprite, (int)palOffset, rom, RomPaletteOffset, mainPalLength);

                    // Gloves are the last 4 bytes of the palette block
                    Array.Copy(sprite, (int)palOffset + mainPalLength, rom, RomGlovesOffset, 4);
                }
                else if (palLength > 0)
                {
                    Array.Copy(sprite, (int)palOffset, rom, RomPaletteOffset, palLength);
                }
            }
            else if (ext == ".spr")
            {
                var sprite = File.ReadAllBytes(spritePath);

                if (sprite.Length < RomGfxMaxLength)
                    return $"Legacy .spr file must be at least 0x7000 ({RomGfxMaxLength}) bytes.";
                if (RomGfxOffset + RomGfxMaxLength > rom.Length)
                    return "ROM file is too small to receive sprite pixel data.";

                Array.Copy(sprite, 0, rom, RomGfxOffset, RomGfxMaxLength);
            }
            else
            {
                return "Unsupported sprite format.";
            }

            // Write to a temp file first, then replace atomically to avoid
            // leaving a corrupt ROM if the write is interrupted (disk full, I/O error).
            string tempPath = romDestPath + ".tmp";
            try
            {
                File.WriteAllBytes(tempPath, rom);
                File.Move(tempPath, romDestPath, overwrite: true);
            }
            catch
            {
                // Clean up the temp file on failure
                try { File.Delete(tempPath); } catch { }
                throw;
            }

            return null; // success
        }
        catch (Exception ex)
        {
            return $"Failed to apply sprite: {ex.Message}";
        }
    }

    /// <summary>
    /// If a backup sidecar exists, copies its regions back into <paramref name="rom"/>;
    /// otherwise writes the ROM's current regions out as the backup.
    /// </summary>
    private static void RestoreOrBackupOriginalRegions(byte[] rom, string romPath)
    {
        const int backupSize = RomGfxMaxLength + RomPaletteLength + 4;

        // ROMs smaller than the sprite regions can't be sprite-patched anyway;
        // the per-sprite bounds checks in Apply will produce the real error.
        if (rom.Length < RomGfxOffset + RomGfxMaxLength ||
            rom.Length < RomPaletteOffset + RomPaletteLength ||
            rom.Length < RomGlovesOffset + 4)
            return;

        string backupPath = romPath + BackupExtension;

        if (File.Exists(backupPath))
        {
            var backup = File.ReadAllBytes(backupPath);
            if (backup.Length == backupSize)
            {
                Array.Copy(backup, 0, rom, RomGfxOffset, RomGfxMaxLength);
                Array.Copy(backup, RomGfxMaxLength, rom, RomPaletteOffset, RomPaletteLength);
                Array.Copy(backup, RomGfxMaxLength + RomPaletteLength, rom, RomGlovesOffset, 4);
                return;
            }
            // Wrong size — stale/corrupt backup; fall through and rewrite it from the ROM.
        }

        var fresh = new byte[backupSize];
        Array.Copy(rom, RomGfxOffset, fresh, 0, RomGfxMaxLength);
        Array.Copy(rom, RomPaletteOffset, fresh, RomGfxMaxLength, RomPaletteLength);
        Array.Copy(rom, RomGlovesOffset, fresh, RomGfxMaxLength + RomPaletteLength, 4);
        File.WriteAllBytes(backupPath, fresh);
    }
}
