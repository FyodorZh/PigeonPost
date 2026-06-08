namespace PigeonPost.Bridge;

public interface IPacketBuffer
{
    int Capacity { get; }
    int Count { get; }
    int TotalBytes { get; }
    long DroppedPackets { get; }
    bool TryEnqueue(byte[] packet);
    bool TryDequeue(out byte[] packet);
}
