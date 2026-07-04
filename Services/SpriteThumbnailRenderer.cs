using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LTTPEnhancementTools.Services;

/// <summary>
/// Renders a Link-standing-pose thumbnail PNG directly from ZSPR pixel data, for
/// sprites whose preview image is missing from alttpr.com (~18% of the catalog).
/// The sheet is SNES 4bpp planar: 8x8 tiles of 32 bytes, 16 tiles per sheet row.
/// The standing pose is head block (0,0) drawn over body block (0,1), body offset
/// 8px down — the same composite the official previews use. Palette is the green
/// mail row: 15 SNES 15-bit BGR colors, palette index 0 transparent.
/// </summary>
public static class SpriteThumbnailRenderer
{
    private const int SheetTilesPerRow = 16;
    private const int TileBytes = 32;

    // ZSPR header field offsets (same layout as SpriteApplier)
    private const int GfxOffsetPos     = 9;
    private const int GfxLengthPos     = 13;
    private const int PaletteOffsetPos = 15;
    private const int PaletteLengthPos = 19;
    private const int MinHeaderSize    = 21;

    /// <summary>
    /// Renders a 16x24 standing-Link preview (scaled by <paramref name="scale"/>)
    /// as PNG bytes, or null if the ZSPR can't be parsed.
    /// </summary>
    public static byte[]? TryRenderPreviewPng(byte[] zspr, int scale = 2)
    {
        try
        {
            if (!TryParse(zspr, out var gfx, out var palette))
                return null;

            var indexed = new byte[16 * 24];
            DrawBlock(gfx, blockCol: 0, blockRow: 1, indexed, canvasW: 16, dx: 0, dy: 8); // body (stand, facing down)
            DrawBlock(gfx, blockCol: 0, blockRow: 0, indexed, canvasW: 16, dx: 0, dy: 0); // head drawn on top

            return EncodePng(indexed, 16, 24, palette, scale);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Renders the full sprite sheet (128px wide) — used for diagnostics/tests.</summary>
    public static byte[]? TryRenderSheetPng(byte[] zspr, int scale = 1)
    {
        try
        {
            if (!TryParse(zspr, out var gfx, out var palette))
                return null;

            int tiles = gfx.Length / TileBytes;
            int rows = (tiles + SheetTilesPerRow - 1) / SheetTilesPerRow;
            int w = SheetTilesPerRow * 8, h = rows * 8;

            var indexed = new byte[w * h];
            for (int t = 0; t < tiles; t++)
                DrawTile(gfx, t, indexed, w, (t % SheetTilesPerRow) * 8, (t / SheetTilesPerRow) * 8);

            return EncodePng(indexed, w, h, palette, scale);
        }
        catch
        {
            return null;
        }
    }

    // ── Parsing ──────────────────────────────────────────────────────────

    private static bool TryParse(byte[] zspr, out byte[] gfx, out Color[] palette)
    {
        gfx = Array.Empty<byte>();
        palette = Array.Empty<Color>();

        if (zspr.Length < MinHeaderSize) return false;
        if (zspr[0] != 'Z' || zspr[1] != 'S' || zspr[2] != 'P' || zspr[3] != 'R') return false;

        uint gfxOffset   = BitConverter.ToUInt32(zspr, GfxOffsetPos);
        ushort gfxLength = BitConverter.ToUInt16(zspr, GfxLengthPos);
        uint palOffset   = BitConverter.ToUInt32(zspr, PaletteOffsetPos);
        ushort palLength = BitConverter.ToUInt16(zspr, PaletteLengthPos);

        if (gfxLength == 0 || gfxOffset + gfxLength > zspr.Length) return false;
        if (palLength < 30 || palOffset + 30 > zspr.Length) return false; // need the 15-color green mail row

        gfx = new byte[gfxLength];
        Array.Copy(zspr, (int)gfxOffset, gfx, 0, gfxLength);

        palette = new Color[16];
        palette[0] = Colors.Transparent;
        for (int i = 0; i < 15; i++)
        {
            ushort c = BitConverter.ToUInt16(zspr, (int)palOffset + i * 2);
            palette[i + 1] = FromSnesColor(c);
        }
        return true;
    }

    /// <summary>SNES 15-bit BGR (bbbbbgggggrrrrr) → 8-bit RGB.</summary>
    private static Color FromSnesColor(ushort c)
    {
        byte To8(int v) => (byte)((v << 3) | (v >> 2));
        return Color.FromRgb(To8(c & 0x1F), To8((c >> 5) & 0x1F), To8((c >> 10) & 0x1F));
    }

    // ── Drawing ──────────────────────────────────────────────────────────

    /// <summary>Draws a 16x16 block (2x2 tiles) from sheet block coordinates.</summary>
    private static void DrawBlock(byte[] gfx, int blockCol, int blockRow, byte[] canvas, int canvasW, int dx, int dy)
    {
        int baseTile = blockRow * 2 * SheetTilesPerRow + blockCol * 2;
        DrawTile(gfx, baseTile,                        canvas, canvasW, dx,     dy);
        DrawTile(gfx, baseTile + 1,                    canvas, canvasW, dx + 8, dy);
        DrawTile(gfx, baseTile + SheetTilesPerRow,     canvas, canvasW, dx,     dy + 8);
        DrawTile(gfx, baseTile + SheetTilesPerRow + 1, canvas, canvasW, dx + 8, dy + 8);
    }

    /// <summary>Decodes one SNES 4bpp planar tile; palette index 0 is transparent (not drawn).</summary>
    private static void DrawTile(byte[] gfx, int tileIndex, byte[] canvas, int canvasW, int dx, int dy)
    {
        int off = tileIndex * TileBytes;
        if (off < 0 || off + TileBytes > gfx.Length) return;

        for (int y = 0; y < 8; y++)
        {
            byte p0 = gfx[off + y * 2];
            byte p1 = gfx[off + y * 2 + 1];
            byte p2 = gfx[off + 16 + y * 2];
            byte p3 = gfx[off + 16 + y * 2 + 1];

            for (int x = 0; x < 8; x++)
            {
                int bit = 7 - x;
                int v = ((p0 >> bit) & 1)
                      | (((p1 >> bit) & 1) << 1)
                      | (((p2 >> bit) & 1) << 2)
                      | (((p3 >> bit) & 1) << 3);
                if (v != 0)
                    canvas[(dy + y) * canvasW + dx + x] = (byte)v;
            }
        }
    }

    // ── Encoding ─────────────────────────────────────────────────────────

    private static byte[] EncodePng(byte[] indexed, int width, int height, Color[] palette, int scale)
    {
        scale = Math.Max(1, scale);
        int w = width * scale, h = height * scale;
        var px = new byte[w * h * 4]; // BGRA32

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                byte idx = indexed[(y / scale) * width + (x / scale)];
                if (idx == 0) continue; // transparent
                var c = palette[idx];
                int o = (y * w + x) * 4;
                px[o] = c.B; px[o + 1] = c.G; px[o + 2] = c.R; px[o + 3] = 255;
            }
        }

        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
