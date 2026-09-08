namespace ByteRadio.Messaging;

public interface IMessageBroadcaster
{
    Task BroadcastAsync(byte[] data, CancellationToken cancellationToken = default);
}
