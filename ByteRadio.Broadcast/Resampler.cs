
using Microsoft.Extensions.Logging;
using NAudio.MediaFoundation;
using NAudio.Wave;
using System.IO;

namespace ByteRadio.Broadcast;

public class Resampler : IDisposable
{
    private static readonly object StartupLock = new();
    private static bool _mediaFoundationStarted;

    private readonly WaveFormat _inputFormat;
    private readonly WaveFormat _outputFormat;
    private readonly ILogger<Resampler> _logger;
    private readonly bool _passthrough;

    private BufferedWaveProvider? _sourceProvider;
    private MediaFoundationResampler? _resampler;
    private bool _disposed;

    public Resampler(WaveFormat input, WaveFormat output, ILogger<Resampler> logger)
    {
        _inputFormat = input;
        _outputFormat = output;
        _logger = logger;
        _passthrough = input.Equals(output);
    }

    internal byte[] ResampleRawPcmData(byte[] buffer)
    {
        if (_passthrough || buffer.Length == 0)
        {
            return buffer;
        }

        try
        {
            var (sourceProvider, resampler) = GetOrCreateResampler();

            sourceProvider.AddSamples(buffer, 0, buffer.Length);

            using var output = new MemoryStream();
            var readBuffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = resampler.Read(readBuffer.AsSpan())) > 0)
            {
                output.Write(readBuffer, 0, bytesRead);
            }

            return output.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resampling PCM data. Source length: {sourceLength}", buffer.Length);
            return [];
        }
    }

    private (BufferedWaveProvider Source, MediaFoundationResampler Resampler) GetOrCreateResampler()
    {
        if (_resampler is null || _sourceProvider is null)
        {
            EnsureMediaFoundationStarted();

            _sourceProvider = new BufferedWaveProvider(_inputFormat, TimeSpan.FromSeconds(5))
            {
                DiscardOnBufferOverflow = true,
                ReadFully = false
            };

            _resampler = new MediaFoundationResampler(_sourceProvider, _outputFormat)
            {
                ResamplerQuality = 60
            };

            _logger.LogDebug("Created MediaFoundationResampler with src: {src} and dst: {dst}", _inputFormat, _outputFormat);
        }

        return (_sourceProvider, _resampler);
    }

    private static void EnsureMediaFoundationStarted()
    {
        if (_mediaFoundationStarted)
        {
            return;
        }

        lock (StartupLock)
        {
            if (_mediaFoundationStarted)
            {
                return;
            }

            MediaFoundationApi.Startup();
            _mediaFoundationStarted = true;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _resampler?.Dispose();
        _resampler = null;
        _sourceProvider = null;
        _disposed = true;
    }
}
