namespace ByteRadio.Messaging;

public class LocalTestRabbitMqOptionsProvider : IRabbitMqOptionsProvider
{
    public async Task<RabbitMqOptions> RequestOptions()
    {
        return await Task.FromResult(new RabbitMqOptions(
            hostName: "localhost",
            port: 5672,
            userName: "guest",
            password: "guest",
            queueName: "live-stream-track-data"
        ));
    }
}