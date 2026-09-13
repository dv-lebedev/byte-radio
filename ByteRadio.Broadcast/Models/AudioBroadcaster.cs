using ByteRadio.Broadcast.Utils;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using System.Net.WebSockets;
using System.Threading.Channels;

namespace ByteRadio.Broadcast.Models;

public sealed class AudioBroadcaster : IAsyncDisposable
{
    private readonly ILogger<AudioBroadcaster> _logger;
    private readonly ISessionData _sessionData;
    private readonly Uri _webSocketUrl;
    private Channel<byte[]>? _rawQueue;
    private Channel<byte[]>? _sendQueue;

    private WasapiLoopbackCapture? _capture;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _sendLoopTask;
    private Task? _converterLoopTask;
    private long _totalBytesSent;
    private PcmToMp3Converter? _pcmToMp3Converter;

    public bool IsRunning { get; private set; }
    public long TotalBytesSent => Interlocked.Read(ref _totalBytesSent);

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<Exception>? ErrorOccurred;

    public AudioBroadcaster(ILogger<AudioBroadcaster> logger, ISessionData sessionData, IApiRouter apiRouter)
    {
        _logger = logger;
        _sessionData = sessionData;
        _webSocketUrl = apiRouter.WebSocket;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }
        
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ct = _cts.Token;

        Interlocked.Exchange(ref _totalBytesSent, 0);

        _rawQueue = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
        _sendQueue = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        _webSocket = new ClientWebSocket();

        _logger.LogDebug("AudioBroadcast for {url}: starting...", _webSocketUrl);
        RaiseStatus("Connecting...");

        _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_sessionData.Token}");
        await _webSocket.ConnectAsync(_webSocketUrl, ct);

        _logger.LogDebug("AudioBroadcast: connected");
        RaiseStatus("Connected");

        _sendLoopTask = Task.Run(() => SendLoopAsync(ct), ct);

        //TODO: WasapiLoopbackCapture is obsolete
        _capture = new WasapiLoopbackCapture();
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;

        _pcmToMp3Converter = new PcmToMp3Converter(_capture.WaveFormat);
        _converterLoopTask = Task.Run(() => ConverterLoopAsync(ct), ct);

        _capture.StartRecording();

        IsRunning = true;
        _logger.LogDebug("Capturing system audio");
        RaiseStatus("Capturing system audio");
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;

        _capture?.StopRecording();

        _rawQueue?.Writer.TryComplete();
        _sendQueue?.Writer.TryComplete();

        _cts?.Cancel();

        if (_converterLoopTask is not null)
        {
            try
            {
                await _converterLoopTask;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error await _converterLoopTask");
            }
        }

        if (_sendLoopTask is not null)
        {
            try
            {
                await _sendLoopTask;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error await _sendLoopTask");
            }
        }

        if (_webSocket is { State: WebSocketState.Open })
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Stopping", CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing socket in AudioBroadcaster");
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        CleanUpCapture();

        _pcmToMp3Converter?.Dispose();
        _pcmToMp3Converter = null;

        _webSocket?.Dispose();
        _webSocket = null;

        _converterLoopTask = null;

        _cts?.Dispose();
        _cts = null;

        _logger.LogDebug("AudioBroadcaster: stop");
        RaiseStatus("Stopped");
    }

    private void CleanUpCapture()
    {
        if (_capture is null)
        {
            return;
        }

        _capture.DataAvailable -= OnDataAvailable;
        _capture.RecordingStopped -= OnRecordingStopped;
        _capture.Dispose();
        _capture = null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
        {
            return;
        }

        // Keep this callback as cheap as possible: WASAPI loopback capture runs it on a
        // real-time audio thread, and any heavy work here (like resampling) causes the
        // internal capture buffer to overrun, which is heard as crackling/glitches.
        var buffer = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);

        _rawQueue?.Writer.TryWrite(buffer);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            ErrorOccurred?.Invoke(this, e.Exception);
        }
    }

    private async Task ConverterLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var buffer in _rawQueue?.Reader.ReadAllAsync(cancellationToken) 
                ?? AsyncEnumerable.Empty<byte[]>())
            {
                var data = _pcmToMp3Converter?.Convert(buffer) ?? [];
                if (data.Length == 0)
                {
                    continue;
                }

                _sendQueue?.Writer.TryWrite(data);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in {nameof(AudioBroadcaster)}.{nameof(ConverterLoopAsync)}");
        }
        finally
        {
            _logger.LogDebug("ConverterLoopAsync: completed");
        }
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var buffer in _sendQueue?.Reader.ReadAllAsync(cancellationToken) 
                ?? AsyncEnumerable.Empty<byte[]>())
            {
                if (_webSocket is not { State: WebSocketState.Open })
                {
                    continue;
                }

                await _webSocket.SendAsync(
                    new ArraySegment<byte>(buffer),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    cancellationToken);

                Interlocked.Add(ref _totalBytesSent, buffer.Length);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in SendLoopAsync");
            ErrorOccurred?.Invoke(this, ex);
        }
        finally
        {
            _logger.LogDebug("SendLoopAsync: completed");
        }
    }

    private void RaiseStatus(string status) => StatusChanged?.Invoke(this, status);

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}