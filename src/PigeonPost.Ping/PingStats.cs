namespace PigeonPost.Ping;

internal sealed class PingStats
{
    private readonly object _lock = new();
    private long _transmitted;
    private long _received;
    private long _corrupted;
    private double _minRtt = double.MaxValue;
    private double _maxRtt;
    private double _totalRtt;

    public void RecordSent()
    {
        lock (_lock)
        {
            _transmitted++;
        }
    }

    public void RecordReply(double rtt)
    {
        lock (_lock)
        {
            _received++;
            _totalRtt += rtt;
            if (rtt < _minRtt)
            {
                _minRtt = rtt;
            }
            if (rtt > _maxRtt)
            {
                _maxRtt = rtt;
            }
        }
    }

    public void RecordCorrupted()
    {
        lock (_lock)
        {
            _corrupted++;
        }
    }

    public PingSummary Snapshot()
    {
        lock (_lock)
        {
            bool hasReplies = _received > 0;
            return new PingSummary(
                _transmitted,
                _received,
                _corrupted,
                hasReplies ? _minRtt : 0,
                hasReplies ? _totalRtt / _received : 0,
                hasReplies ? _maxRtt : 0);
        }
    }
}

internal readonly record struct PingSummary(
    long Transmitted,
    long Received,
    long Corrupted,
    double MinRtt,
    double AverageRtt,
    double MaxRtt)
{
    public double LossPercent
    {
        get
        {
            if (Transmitted == 0)
            {
                return 0;
            }

            long lost = Transmitted - Received - Corrupted;
            return lost <= 0 ? 0 : lost * 100.0 / Transmitted;
        }
    }
}
