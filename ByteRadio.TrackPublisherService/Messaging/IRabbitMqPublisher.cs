namespace ByteRadio.LiveStreamIngestService.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishAsync(byte[] data, CancellationToken cancellationToken = default);
}
