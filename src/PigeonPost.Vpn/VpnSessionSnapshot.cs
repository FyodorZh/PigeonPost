using System;

namespace PigeonPost.Vpn;

public sealed record VpnSessionSnapshot(
    ConnectionState State,
    DateTime? SessionStart,
    long BytesSent,
    long BytesReceived,
    double SpeedSentBps,
    double SpeedReceivedBps,
    int ReconnectCount);
