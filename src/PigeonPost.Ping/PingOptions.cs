namespace PigeonPost.Ping;

internal enum PingMode
{
    Server,
    Client
}

internal sealed class PingOptions
{
    public PingMode Mode { get; init; }
    public string Url { get; init; } = string.Empty;
    public int IntervalMs { get; init; } = 1000;
    public int PayloadSize { get; init; } = 100;
    public int WaitMs { get; init; } = 5000;
    public int Count { get; init; }
    public bool Verbose { get; init; }
}
