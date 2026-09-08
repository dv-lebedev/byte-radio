namespace ByteRadio.Messaging;

public class RabbitMqOptions
{
    public string HostName { get; }
    public int Port { get; }
    public string UserName { get; }
    public string Password { get; }
    public string QueueName { get; }

    public RabbitMqOptions(string hostName, int port, string userName, string password, string queueName)
    {
        HostName = hostName;
        Port = port;
        UserName = userName;
        Password = password;
        QueueName = queueName;
    }
}