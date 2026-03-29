using System.IO;
using NAudio.Wave;

namespace LTTPEnhancementTools.Services;

/// <summary>
/// Manages a single audio playback channel for MSU-1 PCM preview.
/// MSU-1 PCM format: 8-byte header ("MSU1" + loop point uint32 LE), then raw 44.1kHz 16-bit stereo PCM.
/// </summary>
public class AudioPlayer : IDisposable
{
    private WaveOutEvent? _output;
    private RawSourceWaveStream? _waveStream;
    private FileStream? _stream;
    private bool _disposed;

    public event EventHandler? PlaybackStopped;

    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;

    /// <summary>
    /// Starts playing the given PCM file. Any current playback is stopped first.
    /// Returns an error message if playback cannot start, or null on success.
    /// </summary>
    public string? Play(string pcmPath)
    {
        Stop();

        try
        {
            var stream = new FileStream(pcmPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                stream.Seek(8, SeekOrigin.Begin); // skip MSU-1 header
                var waveFormat = new WaveFormat(44100, 16, 2); // 44.1kHz, 16-bit, stereo
                var waveStream = new RawSourceWaveStream(stream, waveFormat);

                _stream = stream;
                _waveStream = waveStream;
                _output = new WaveOutEvent();
                _output.Init(waveStream);
                _output.PlaybackStopped += OnOutputPlaybackStopped;
                _output.Play();
                return null;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }
        catch (Exception ex)
        {
            DisposePlayback();
            return $"Playback error: {ex.Message}";
        }
    }

    public void Stop()
    {
        if (_output?.PlaybackState == PlaybackState.Playing)
            _output.Stop();

        DisposePlayback();
    }

    private void OnOutputPlaybackStopped(object? sender, StoppedEventArgs args)
        => PlaybackStopped?.Invoke(this, EventArgs.Empty);

    private void DisposePlayback()
    {
        if (_output is not null)
        {
            _output.PlaybackStopped -= OnOutputPlaybackStopped;
            _output.Dispose();
            _output = null;
        }
        _waveStream?.Dispose();
        _waveStream = null;
        _stream?.Dispose();
        _stream = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DisposePlayback();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
