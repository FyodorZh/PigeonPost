namespace PigeonPost.Tun.Virtual;

public delegate void Ipv4PacketHandler(IPv4 source, IPv4 destination, byte[] data);
