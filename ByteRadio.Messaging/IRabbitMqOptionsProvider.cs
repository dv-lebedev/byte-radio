namespace ByteRadio.Messaging;

public interface IRabbitMqOptionsProvider
{
    public Task<RabbitMqOptions> RequestOptions();
}
