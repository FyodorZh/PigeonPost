using System;

namespace PigeonPost.Bridge;

public sealed class HandshakeRejectedException : Exception
{
    public HandshakeRejectCode RejectCode { get; }

    public HandshakeRejectedException(HandshakeRejectCode rejectCode)
        : base($"Handshake rejected: {rejectCode}")
    {
        RejectCode = rejectCode;
    }

    public HandshakeRejectedException(HandshakeRejectCode rejectCode, string message)
        : base(message)
    {
        RejectCode = rejectCode;
    }
}
