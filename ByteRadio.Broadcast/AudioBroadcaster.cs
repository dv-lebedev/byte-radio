using Microsoft.Extensions.Logging;
using NAudio.Wave;
using System.Net.WebSockets;
using System.Threading.Channels;

namespace ByteRadio.Broadcast;

public sealed class AudioBroadcaster : IAsyncDisposable
{
    private readonly ILoggerFactory _loggerFactory;

    private Channel<byte[]> _rawQueue = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = true
    });

    private Channel<byte[]> _sendQueue = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = true
    });

    private WasapiLoopbackCapture? _capture;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _sendLoopTask;
    private Task? _resampleLoopTask;
    private long _totalBytesSent;

    public bool IsRunning { get; private set; }

    public long TotalBytesSent => Interlocked.Read(ref _totalBytesSent);

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<Exception>? ErrorOccurred;

    private Resampler? _resampler;

    public AudioBroadcaster(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public async Task StartAsync(string webSocketUrl, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
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

        RaiseStatus("Connecting...");
        await _webSocket.ConnectAsync(new Uri(webSocketUrl), cancellationToken);
        RaiseStatus("Connected");

        _sendLoopTask = Task.Run(() => SendLoopAsync(ct), ct);

        _capture = new WasapiLoopbackCapture();
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;

        _resampler = new Resampler(_capture.WaveFormat, WaveFormat.CreateIeeeFloatWaveFormat(32000, 2), _loggerFactory.CreateLogger<Resampler>());
        _resampleLoopTask = Task.Run(() => ResampleLoopAsync(ct), ct);

        _capture.StartRecording();

        IsRunning = true;
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

        _rawQueue.Writer.TryComplete();

        _cts?.Cancel();

        if (_resampleLoopTask is not null)
        {
            try
            {
                await _resampleLoopTask;
            }
            catch (OperationCanceledException)
            {
                // expected on stop
            }
        }

        if (_sendLoopTask is not null)
        {
            try
            {
                await _sendLoopTask;
            }
            catch (OperationCanceledException)
            {
                // expected on stop
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
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        CleanUpCapture();

        _resampler?.Dispose();
        _resampler = null;

        _webSocket?.Dispose();
        _webSocket = null;

        _resampleLoopTask = null;

        _cts?.Dispose();
        _cts = null;

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

        _rawQueue.Writer.TryWrite(buffer);
    }

    private async Task ResampleLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var buffer in _rawQueue.Reader.ReadAllAsync(cancellationToken))
            {
                //TODO: resampling needs more work
                //var data = _resampler?.ResampleRawPcmData(buffer) ?? buffer;
                //if (data.Length == 0)
                //{
                //    continue;
                //}

                _sendQueue.Writer.TryWrite(buffer);
            }
        }
        catch (OperationCanceledException)
        {
            // expected on stop
        }
        finally
        {
            _sendQueue.Writer.TryComplete();
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            ErrorOccurred?.Invoke(this, e.Exception);
        }
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var buffer in _sendQueue.Reader.ReadAllAsync(cancellationToken))
            {
                if (_webSocket is not { State: WebSocketState.Open })
                {
                    continue;
                }

                try
                {
                    await _webSocket.SendAsync(
                        new ArraySegment<byte>(buffer),
                        WebSocketMessageType.Binary,
                        endOfMessage: true,
                        cancellationToken);

                    Interlocked.Add(ref _totalBytesSent, buffer.Length);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, ex);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected on stop
        }
    }

    private void RaiseStatus(string status) => StatusChanged?.Invoke(this, status);

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
