using System;
using System.Collections.Generic;

namespace PigeonPost;

public sealed class BridgeConfiguration
{
    public Role Role { get; init; }
    public IReadOnlyList<string> TunNames { get; init; } = Array.Empty<string>();
    public string PontifexUrl { get; init; } = string.Empty;
    public int BufferSizeBytes { get; init; } = 10 * 1024 * 1024;
    public bool Verbose { get; init; }
    public string? ClientId { get; init; }
    public int DebugClientCount { get; init; } = 1;
}
