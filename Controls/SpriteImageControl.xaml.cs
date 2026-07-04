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

    private static async Task<BitmapImage?> LoadBitmapAsync(string url, CancellationToken ct)
    {
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = await File.ReadAllBytesAsync(url, ct);
            return Services.PreviewCache.TryDecode(bytes);
        }

        // EnsureCachedAsync self-heals: a cached file that no longer decodes (truncated
        // write from an old crash) is deleted and re-downloaded, and fresh bytes are only
        // cached after decoding successfully — a corrupt file can never wedge a sprite.
        var cachePath = await Services.PreviewCache.EnsureCachedAsync(url, Http, ct);
        if (cachePath is null) return null;

        return Services.PreviewCache.TryDecode(await File.ReadAllBytesAsync(cachePath, ct));
    }
}
