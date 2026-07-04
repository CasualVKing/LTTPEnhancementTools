using System.Net;
using System.Net.Http;
using LTTPEnhancementTools.Services;

namespace LTTPEnhancementTools.Tests;

public class PreviewCacheTests : IDisposable
{
    // Minimal valid 1x1 PNG
    private static readonly byte[] ValidPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static readonly byte[] Garbage = { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01 };

    private readonly string _tempDir;
    private readonly List<string> _cacheFilesToClean = new();

    public PreviewCacheTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PreviewCacheTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
        foreach (var f in _cacheFilesToClean)
            try { File.Delete(f); } catch { }
    }

    /// <summary>Unique fake URL so each test gets its own file in the real cache dir; cleaned up on dispose.</summary>
    private string MakeTrackedUrl()
    {
        string url = $"https://example.invalid/{Guid.NewGuid():N}.png";
        _cacheFilesToClean.Add(PreviewCache.GetPath(url));
        return url;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly byte[] _body;
        public int Requests { get; private set; }
        public StubHandler(byte[] body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_body)
            });
        }
    }

    // ── TryDecode ─────────────────────────────────────────────────────

    [Fact]
    public void TryDecode_ValidPng_ReturnsBitmap()
    {
        Assert.NotNull(PreviewCache.TryDecode(ValidPng));
    }

    [Fact]
    public void TryDecode_Garbage_ReturnsNull()
    {
        Assert.Null(PreviewCache.TryDecode(Garbage));
    }

    [Fact]
    public void TryDecode_TruncatedPng_ReturnsNull()
    {
        // Simulates an interrupted cache write — the exact condition that used to
        // wedge a sprite preview on the loading animation forever.
        var truncated = ValidPng[..(ValidPng.Length / 2)];
        Assert.Null(PreviewCache.TryDecode(truncated));
    }

    // ── TrySaveAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task TrySaveAsync_WritesFileAndLeavesNoTempFiles()
    {
        string path = Path.Combine(_tempDir, "img.png");

        bool ok = await PreviewCache.TrySaveAsync(path, ValidPng);

        Assert.True(ok);
        Assert.Equal(ValidPng, File.ReadAllBytes(path));
        Assert.Single(Directory.GetFiles(_tempDir)); // no .tmp leftovers
    }

    // ── EnsureCachedAsync ─────────────────────────────────────────────

    [Fact]
    public async Task EnsureCachedAsync_CorruptCachedFile_RedownloadsAndHeals()
    {
        string url = MakeTrackedUrl();
        string cachePath = PreviewCache.GetPath(url);

        // Plant a corrupt cache file (as if a write was interrupted)
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        File.WriteAllBytes(cachePath, Garbage);

        var handler = new StubHandler(ValidPng);
        using var http = new HttpClient(handler);

        string? result = await PreviewCache.EnsureCachedAsync(url, http);

        Assert.Equal(cachePath, result);
        Assert.Equal(1, handler.Requests); // corrupt cache triggered a re-download
        Assert.NotNull(PreviewCache.TryDecode(File.ReadAllBytes(cachePath))); // healed
    }

    [Fact]
    public async Task EnsureCachedAsync_ValidCachedFile_NoDownload()
    {
        string url = MakeTrackedUrl();
        string cachePath = PreviewCache.GetPath(url);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        File.WriteAllBytes(cachePath, ValidPng);

        var handler = new StubHandler(ValidPng);
        using var http = new HttpClient(handler);

        string? result = await PreviewCache.EnsureCachedAsync(url, http);

        Assert.Equal(cachePath, result);
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task EnsureCachedAsync_DownloadIsNotAnImage_ReturnsNullAndCachesNothing()
    {
        string url = MakeTrackedUrl();
        string cachePath = PreviewCache.GetPath(url);

        var handler = new StubHandler(Garbage); // e.g. an HTML error page
        using var http = new HttpClient(handler);

        string? result = await PreviewCache.EnsureCachedAsync(url, http);

        Assert.Null(result);
        Assert.False(File.Exists(cachePath), "undecodable bytes must never be cached");
    }
}
