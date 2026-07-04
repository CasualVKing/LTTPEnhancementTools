using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LTTPEnhancementTools.Services;

namespace LTTPEnhancementTools.Tests;

public class SpriteThumbnailRendererTests
{
    private const int GfxLen = 0x7000;
    private const int PalLen = 124;
    private const int HeaderSize = 21;

    /// <summary>
    /// Builds a synthetic ZSPR:
    ///  - head block tiles 0 and 16 (left column, y 0-15) filled with palette index 1 (red)
    ///  - body block tiles 32 and 48 (left column, y 8-23 at compose time) filled with index 2 (green)
    ///  - palette: color 1 = SNES red (0x001F), color 2 = SNES green (0x03E0)
    /// </summary>
    private static byte[] BuildSyntheticZspr()
    {
        var data = new byte[HeaderSize + GfxLen + PalLen];
        data[0] = (byte)'Z'; data[1] = (byte)'S'; data[2] = (byte)'P'; data[3] = (byte)'R';
        BitConverter.GetBytes((uint)HeaderSize).CopyTo(data, 9);
        BitConverter.GetBytes((ushort)GfxLen).CopyTo(data, 13);
        BitConverter.GetBytes((uint)(HeaderSize + GfxLen)).CopyTo(data, 15);
        BitConverter.GetBytes((ushort)PalLen).CopyTo(data, 19);

        // 4bpp planar: plane 0 = bytes 2y, plane 1 = bytes 2y+1 (index 1 = plane0, index 2 = plane1)
        void FillTile(int tile, int plane)
        {
            int off = HeaderSize + tile * 32;
            for (int y = 0; y < 8; y++)
                data[off + y * 2 + plane] = 0xFF;
        }
        FillTile(0, 0);   // head, top-left tile, index 1
        FillTile(16, 0);  // head, bottom-left tile, index 1
        FillTile(32, 1);  // body, top-left tile, index 2
        FillTile(48, 1);  // body, bottom-left tile, index 2

        int pal = HeaderSize + GfxLen;
        BitConverter.GetBytes((ushort)0x001F).CopyTo(data, pal);     // color 1: red   (r=31)
        BitConverter.GetBytes((ushort)0x03E0).CopyTo(data, pal + 2); // color 2: green (g=31)
        return data;
    }

    private static byte[] DecodeBgra(byte[] png, out int width, out int height)
    {
        using var ms = new MemoryStream(png);
        var frame = BitmapFrame.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        width = converted.PixelWidth;
        height = converted.PixelHeight;
        var px = new byte[width * height * 4];
        converted.CopyPixels(px, width * 4, 0);
        return px;
    }

    private static (byte b, byte g, byte r, byte a) Pixel(byte[] px, int w, int x, int y)
    {
        int o = (y * w + x) * 4;
        return (px[o], px[o + 1], px[o + 2], px[o + 3]);
    }

    [Fact]
    public void Garbage_ReturnsNull()
    {
        Assert.Null(SpriteThumbnailRenderer.TryRenderPreviewPng(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void GfxRegionExceedsFile_ReturnsNull()
    {
        var zspr = BuildSyntheticZspr();
        BitConverter.GetBytes((uint)(zspr.Length - 10)).CopyTo(zspr, 9); // gfx offset near EOF
        Assert.Null(SpriteThumbnailRenderer.TryRenderPreviewPng(zspr));
    }

    [Fact]
    public void MissingPalette_ReturnsNull()
    {
        var zspr = BuildSyntheticZspr();
        BitConverter.GetBytes((ushort)0).CopyTo(zspr, 19); // palette length 0
        Assert.Null(SpriteThumbnailRenderer.TryRenderPreviewPng(zspr));
    }

    [Fact]
    public void Synthetic_RendersExpectedComposite()
    {
        var png = SpriteThumbnailRenderer.TryRenderPreviewPng(BuildSyntheticZspr(), scale: 1);
        Assert.NotNull(png);

        var px = DecodeBgra(png, out int w, out int h);
        Assert.Equal(16, w);
        Assert.Equal(24, h);

        // Head region: red, opaque
        var head = Pixel(px, w, 0, 0);
        Assert.Equal((0, 0, 255, 255), (head.b, head.g, head.r, head.a));

        // Overlap row (y=8): head is drawn over the body — still red
        var overlap = Pixel(px, w, 0, 8);
        Assert.Equal((0, 0, 255, 255), (overlap.b, overlap.g, overlap.r, overlap.a));

        // Below the head (y=16): body — green
        var body = Pixel(px, w, 0, 16);
        Assert.Equal((0, 255, 0, 255), (body.b, body.g, body.r, body.a));

        // Right half of head row: no tile data — transparent
        var empty = Pixel(px, w, 8, 0);
        Assert.Equal(0, empty.a);
    }

    [Fact]
    public void Scale_ScalesOutputDimensions()
    {
        var png = SpriteThumbnailRenderer.TryRenderPreviewPng(BuildSyntheticZspr(), scale: 4);
        Assert.NotNull(png);
        DecodeBgra(png, out int w, out int h);
        Assert.Equal(64, w);
        Assert.Equal(96, h);
    }

    [Fact]
    public void SheetRender_ProducesFullSheetDimensions()
    {
        var png = SpriteThumbnailRenderer.TryRenderSheetPng(BuildSyntheticZspr(), scale: 1);
        Assert.NotNull(png);
        DecodeBgra(png, out int w, out int h);
        Assert.Equal(128, w);            // 16 tiles wide
        Assert.Equal(GfxLen / 32 / 16 * 8, h); // 56 tile rows
    }
}
