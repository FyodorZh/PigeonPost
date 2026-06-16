namespace PigeonPost.Bridge;

public interface IPacketBuffer
{
    bool TryEnqueue(byte[] packet);
    bool TryDequeue(out byte[] packet);
}
