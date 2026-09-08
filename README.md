# ByteRadio

ByteRadio is a small, end-to-end system for broadcasting a live audio stream (e.g. system/desktop audio) from a Windows machine to any number of listeners in a web browser, in near real time.

It is composed of a Windows desktop capture/broadcast app, a message broker in the middle, and a lightweight ASP.NET Core gateway that fans the audio out to web clients over WebSockets.

## How it works

```
 ┌─────────────────────┐        ┌────────────────────────────┐        ┌───────────────┐        ┌──────────────────────────-┐        ┌────────────────┐
 │ ByteRadio.Broadcast │  WS    │ LiveStreamIngestService    │  AMQP  │   RabbitMQ    │  AMQP  │ ByteRadio.StreamingGateway│  WS    │ Browser client │
 │ (WPF desktop app)   │──────> │  (ingest endpoint)         │──────> │   broker      │──────> │ Service (fan-out gateway) │──────> │ (index.html)   │
 └─────────────────────┘        └────────────────────────────┘        └───────────────┘        └──────────────────────────-┘        └────────────────┘
```

1. **ByteRadio.Broadcast** (WPF) captures system audio via WASAPI loopback, resamples it to a standard PCM format, and streams the raw bytes over a WebSocket connection.
2. **ByteRadio.LiveStreamIngestService** accepts that WebSocket connection, reads the incoming audio chunks, and publishes each chunk as a message onto a RabbitMQ queue.
3. **RabbitMQ** decouples the publisher from any number of gateway instances/consumers.
4. **ByteRadio.StreamingGatewayService** consumes messages from RabbitMQ and re-broadcasts each audio chunk to all currently connected browser clients over their own WebSocket connections. It also serves a minimal HTML/JS player (`wwwroot/index.html`).
5. The **browser client** decodes the incoming raw PCM bytes into an `AudioBuffer` using the Web Audio API and schedules them for gapless playback.

## Technologies used

- **.NET 10** (all projects target `net10.0` / `net10.0-windows`)
- **C#** (latest language features, e.g. primary constructors, collection expressions)
- **WPF** (Windows Presentation Foundation) for the desktop broadcaster app
- **CommunityToolkit.Mvvm** for MVVM (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`)
- **ASP.NET Core** Web API for the publisher and gateway services
- **WebSockets** (`System.Net.WebSockets`, `ClientWebSocket`) for real-time, bidirectional audio streaming
- **RabbitMQ** (`RabbitMQ.Client`) as the message broker decoupling ingest from fan-out
- **NAudio** for audio capture (`WasapiLoopbackCapture`) and resampling (`MediaFoundationResampler`, sample providers)
- **Serilog** for structured logging across all services, integrated through `Microsoft.Extensions.Logging.ILogger<T>`
- **Swagger / OpenAPI** (`Microsoft.OpenApi`, Swashbuckle-style `AddSwaggerGen`) for API documentation
- **ASP.NET Core Health Checks** for service liveness endpoints
- **HTML5 / JavaScript / Web Audio API** for the browser-based live audio player

## Typical flow to run locally

1. Start a RabbitMQ instance (e.g. via Docker) and configure connection details for the services (see `LocalTestRabbitMqOptionsProvider` / `appsettings.json` in each service).
2. Run `ByteRadio.LiveStreamIngestService` — it exposes a WebSocket endpoint that accepts an incoming audio stream and publishes it to RabbitMQ.
3. Run `ByteRadio.StreamingGatewayService` — it consumes from RabbitMQ and serves the web player plus a listener WebSocket endpoint.
4. Run `ByteRadio.Broadcast`, enter the ingest service's WebSocket URL, and click **Start** to begin capturing and streaming system audio.
5. Open the gateway service's web player (`index.html`) in a browser and click **Play** to listen to the live stream.
