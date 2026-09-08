using NAudio.Wave;
using System.IO;
using System.Net.WebSockets;
using System.Threading.Channels;

namespace ByteRadio.Broadcast;

public sealed class AudioBroadcaster : IAsyncDisposable
{
    private readonly Channel<byte[]> _sendQueue = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = true
    });

    private WasapiLoopbackCapture? _capture;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _sendLoopTask;

    public bool IsRunning { get; private set; }

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<Exception>? ErrorOccurred;

    private Resampler _resampler;

    public async Task StartAsync(string webSocketUrl, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _webSocket = new ClientWebSocket();

        RaiseStatus("Connecting...");
        await _webSocket.ConnectAsync(new Uri(webSocketUrl), cancellationToken);
        RaiseStatus("Connected");

        _sendLoopTask = Task.Run(() => SendLoopAsync(ct), ct);

        _capture = new WasapiLoopbackCapture();
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
        _capture.StartRecording();

        _resampler = new Resampler(_capture.WaveFormat, new WaveFormat(44100, 16, 2));

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

        _cts?.Cancel();

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

        _webSocket?.Dispose();
        _webSocket = null;

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

        var buffer = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);

        //var data = _resampler.ResampleRawPcmData(buffer, _capture.WaveFormat.Channels);

        _sendQueue.Writer.TryWrite(buffer);
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
