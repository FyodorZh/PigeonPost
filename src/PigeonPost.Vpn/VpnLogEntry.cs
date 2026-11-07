using System;

namespace PigeonPost.Vpn;

public sealed record VpnLogEntry(DateTime Timestamp, string Message, VpnLogLevel Level);
