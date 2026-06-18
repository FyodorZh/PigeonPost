using System;
using System.Collections.Generic;
using System.Threading;

namespace PigeonPost.Vpn;

public sealed class SpeedHistoryBuffer
{
    private readonly Lock _lock = new();
    private readonly double[] _sent;
    private readonly double[] _received;
    private int _index;

    public int Capacity => 30;

    public SpeedHistoryBuffer()
    {
        _sent = new double[Capacity];
        _received = new double[Capacity];
    }

    public void AddSample(double sentBytesPerSecond, double receivedBytesPerSecond)
    {
        lock (_lock)
        {
            _sent[_index] = sentBytesPerSecond;
            _received[_index] = receivedBytesPerSecond;
            _index = (_index + 1) % Capacity;
        }
    }

    public IReadOnlyList<double> SentHistory
    {
        get
        {
            lock (_lock)
            {
                var result = new double[Capacity];
                for (var i = 0; i < Capacity; i++)
                    result[i] = _sent[(_index + i) % Capacity];
                return result;
            }
        }
    }

    public IReadOnlyList<double> ReceivedHistory
    {
        get
        {
            lock (_lock)
            {
                var result = new double[Capacity];
                for (var i = 0; i < Capacity; i++)
                    result[i] = _received[(_index + i) % Capacity];
                return result;
            }
        }
    }
}
