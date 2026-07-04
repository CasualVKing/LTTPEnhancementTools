using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace LTTPEnhancementTools.Controls;

/// <summary>
/// Displays a sprite preview image with an animated triforce loading indicator.
/// Downloads via HttpClient (not BitmapImage.UriSource) so HTTPS works reliably in .NET 8.
/// Preview images are cached to disk so subsequent opens load instantly and work offline.
/// </summary>
public partial class SpriteImageControl : UserControl
{
    private static readonly HttpClient Http = Services.SharedHttp.Client;

    public static readonly DependencyProperty ImageUrlProperty =
        DependencyProperty.Register(
            nameof(ImageUrl), typeof(string), typeof(SpriteImageControl),
            new PropertyMetadata(null, OnImageUrlChanged));

    public string? ImageUrl
    {
        get => (string?)GetValue(ImageUrlProperty);
        set => SetValue(ImageUrlProperty, value);
    }

    /// <summary>
    /// Optional URL of the sprite's .zspr file. When set and the preview image is
    /// unavailable (missing from alttpr's server), a thumbnail is rendered locally
    /// from the sprite's own pixel data instead of showing "no preview".
    /// </summary>
    public static readonly DependencyProperty SpriteFileUrlProperty =
        DependencyProperty.Register(
            nameof(SpriteFileUrl), typeof(string), typeof(SpriteImageControl),
            new PropertyMetadata(null));

    public string? SpriteFileUrl
    {
        get => (string?)GetValue(SpriteFileUrlProperty);
        set => SetValue(SpriteFileUrlProperty, value);
    }

    private static void OnImageUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SpriteImageControl)d).LoadImage(e.NewValue as string);

    // Per-control CTS so a URL change cancels the in-flight download
    private CancellationTokenSource? _cts;

    public SpriteImageControl()
    {
        InitializeComponent();
    }

    private async void LoadImage(string? url)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        var cts = new CancellationTokenSource();
        _cts = cts;

        SpriteImage.Source = null;
        ErrorGlyph.Visibility = Visibility.Collapsed;
        TriforceCanvas.Visibility = Visibility.Visible;

        if (string.IsNullOrEmpty(url)) return;

        try
        {
            var bmp = await LoadBitmapAsync(url, cts.Token);
            if (cts.IsCancellationRequested) return;

            if (bmp is null)
            {
                ShowError();
                return;
            }

            SpriteImage.Source = bmp;
            TriforceCanvas.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch
        {
            // Download/read failed — show the error glyph rather than spinning forever.
            // The load retries naturally when the (virtualized) item is realized again.
            if (!cts.IsCancellationRequested)
                ShowError();
        }
    }

    private void ShowError()
    {
        TriforceCanvas.Visibility = Visibility.Collapsed;
        ErrorGlyph.Visibility = Visibility.Visible;
    }

    private async Task<BitmapImage?> LoadBitmapAsync(string url, CancellationToken ct)
    {
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = await File.ReadAllBytesAsync(url, ct);
            return Services.PreviewCache.TryDecode(bytes);
        }

        // EnsureCachedAsync self-heals: a cached file that no longer decodes (truncated
        // write from an old crash) is deleted and re-downloaded, and fresh bytes are only
        // cached after decoding successfully — a corrupt file can never wedge a sprite.
        try
        {
            var cachePath = await Services.PreviewCache.EnsureCachedAsync(url, Http, ct);
            if (cachePath is not null)
                return Services.PreviewCache.TryDecode(await File.ReadAllBytesAsync(cachePath, ct));
        }
        catch (OperationCanceledException) { throw; }
        catch { /* preview unavailable — try rendering from the sprite data below */ }

        return await TryGenerateFromSpriteAsync(url, ct);
    }

    /// <summary>
    /// Fallback for previews missing from alttpr's server (~18% of the catalog):
    /// downloads the .zspr and renders the standing-pose thumbnail from its own
    /// pixel data. The generated PNG is saved at the preview's cache path, so
    /// subsequent loads are plain cache hits.
    /// </summary>
    private async Task<BitmapImage?> TryGenerateFromSpriteAsync(string previewUrl, CancellationToken ct)
    {
        var fileUrl = SpriteFileUrl;
        if (string.IsNullOrEmpty(fileUrl)) return null;

        try
        {
            string zsprCachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LTTPEnhancementTools", "SpriteCache",
                Path.GetFileName(new Uri(fileUrl).LocalPath));

            byte[] zspr;
            if (File.Exists(zsprCachePath))
            {
                zspr = await File.ReadAllBytesAsync(zsprCachePath, ct);
            }
            else
            {
                zspr = await Http.GetByteArrayAsync(fileUrl, ct);
                _ = Services.PreviewCache.TrySaveAsync(zsprCachePath, zspr);
            }

            var png = Services.SpriteThumbnailRenderer.TryRenderPreviewPng(zspr, scale: 4);
            if (png is null) return null;

            _ = Services.PreviewCache.TrySaveAsync(Services.PreviewCache.GetPath(previewUrl), png);
            return Services.PreviewCache.TryDecode(png);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return null;
        }
    }
}
