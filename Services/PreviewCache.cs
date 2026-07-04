using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace LTTPEnhancementTools.Services;

/// <summary>
/// Maps a sprite preview URL to its on-disk cache path. Shared by MainWindow and
/// SpriteImageControl so an image downloaded while browsing is reused without a
/// second HTTP request. Fallback names use SHA256 (never string.GetHashCode(),
/// which is randomized per process and would break the cache across runs).
/// </summary>
public static class PreviewCache
{
    public static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LTTPEnhancementTools", "SpriteCache", "Previews");

    public static string GetPath(string url)
    {
        try
        {
            var fileName = Path.GetFileName(new Uri(url).LocalPath);
            fileName = string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));
            if (!string.IsNullOrEmpty(fileName))
                return Path.Combine(Dir, fileName);
        }
        catch
        {
            // Not a parseable URL — fall through to the hash-based name.
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return Path.Combine(Dir, Convert.ToHexString(hash)[..16] + ".png");
    }

    /// <summary>
    /// Decodes image bytes into a frozen BitmapImage, or returns null if the bytes
    /// are not a valid image (truncated download, corrupt cache file, HTML error page).
    /// </summary>
    public static BitmapImage? TryDecode(byte[] bytes, int decodePixelWidth = 64)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.DecodePixelWidth = decodePixelWidth;
            bmp.StreamSource = ms;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Writes cache bytes atomically (unique temp file + move) so an interrupted or
    /// concurrent write can never leave a truncated image at the cache path.
    /// </summary>
    public static async Task<bool> TrySaveAsync(string path, byte[] bytes)
    {
        string tempPath = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(tempPath, bytes);
            File.Move(tempPath, path, overwrite: true);
            return true;
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }
            return false;
        }
    }

    /// <summary>
    /// Returns the local cache path for the URL, downloading if needed. A cached file
    /// that no longer decodes (e.g. a truncated write from an old crash) is deleted and
    /// re-downloaded — the cache self-heals instead of failing forever. Bytes are only
    /// cached after they decode successfully. Returns null if the URL can't produce a
    /// valid image; network errors propagate to the caller.
    /// </summary>
    public static async Task<string?> EnsureCachedAsync(string url, HttpClient http, CancellationToken ct = default)
    {
        string cachePath = GetPath(url);

        if (File.Exists(cachePath))
        {
            try
            {
                var cached = await File.ReadAllBytesAsync(cachePath, ct);
                if (TryDecode(cached) is not null)
                    return cachePath;
            }
            catch (OperationCanceledException) { throw; }
            catch { /* unreadable — treat as corrupt */ }

            try { File.Delete(cachePath); } catch { }
        }

        var bytes = await http.GetByteArrayAsync(url, ct);
        if (TryDecode(bytes) is null)
            return null;

        return await TrySaveAsync(cachePath, bytes) ? cachePath : null;
    }
}
