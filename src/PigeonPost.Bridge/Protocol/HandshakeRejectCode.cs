namespace PigeonPost.Bridge;

public enum HandshakeRejectCode : byte
{
    None = 0,
    DuplicateClientId = 1,
    DuplicateHostIp = 2,
    InvalidHandshake = 3,
    UnsupportedPacketFamily = 4,
    ServerShuttingDown = 5
}
