using System;
using System.Threading;

namespace PigeonPost.Vpn;

internal sealed class ProbeScheduler : IDisposable
{
    private readonly ProbeTunDevice _probeTun;
    private readonly RuntimeLogger _log;
    private readonly uint _sourceIp;
    private Timer? _timer;
    private Timer? _timeoutTimer;
    private bool _disposed;

    public ProbeScheduler(ProbeTunDevice probeTun, RuntimeLogger log, uint sourceIp)
    {
        _probeTun = probeTun ?? throw new ArgumentNullException(nameof(probeTun));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _sourceIp = sourceIp;
    }

    public void Start()
    {
        _probeTun.ProbeReplyReceived += OnProbeReply;
        _timer = new Timer(SendProbe, null, TimeSpan.Zero, ProbeTunDevice.ProbeInterval);
        _timeoutTimer = new Timer(CheckTimeouts, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _timeoutTimer?.Dispose();
        _timeoutTimer = null;
        _probeTun.ProbeReplyReceived -= OnProbeReply;
        _probeTun.ClearProbes();
    }

    private void SendProbe(object? state)
    {
        ushort id = _probeTun.GenerateNextId();
        ushort sequence = _probeTun.GenerateNextSequence();
        var packet = IcmpHelper.CreateEchoRequest(_sourceIp, id, sequence);
        _probeTun.EnqueueProbe(packet);
        _probeTun.RecordSentProbe(id, sequence);
        _log.i($"Probe sent #{sequence} to 1.1.1.1");
    }

    private void OnProbeReply(ushort id, ushort sequence, long rttMs)
    {
        _log.i($"Probe reply #{sequence} from 1.1.1.1 in {rttMs}ms");
    }

    private void CheckTimeouts(object? state)
    {
        _probeTun.CheckTimeouts(ProbeTunDevice.Timeout, seq =>
        {
            _log.w($"Probe #{seq} timed out");
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
    }
}
