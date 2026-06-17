namespace PigeonPost.Tun;

public sealed class Ipv4PacketInfo
{
    public uint SourceAddress { get; init; }
    public uint DestinationAddress { get; init; }
    public int HeaderLength { get; init; }
    public byte Protocol { get; init; }
}
