namespace PigeonPost.Bridge.Protocol;

public sealed record HandshakeAck
{
    public HandshakeAckStatus Status { get; }
    public HandshakeRejectCode RejectCode { get; }

    public HandshakeAck(HandshakeAckStatus status, HandshakeRejectCode rejectCode = HandshakeRejectCode.None)
    {
        Status = status;
        RejectCode = rejectCode;
    }

    public static HandshakeAck Accepted() => new(HandshakeAckStatus.Accepted);

    public static HandshakeAck Rejected(HandshakeRejectCode code) => new(HandshakeAckStatus.Rejected, code);
}
