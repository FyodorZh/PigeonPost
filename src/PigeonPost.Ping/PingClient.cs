using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Handlers.Client;
using Pontifex.Transports.Tcp;
using Pontifex.Utils;
using Scriba;

namespace PigeonPost.Ping;

internal sealed class PingClient
{
    private readonly PingOptions _options;
    private readonly ILogger _logger;
    private readonly PingLog _log;
    private readonly ulong _clientId = PingProtocol.CreateId();
    private readonly TransportFactory _transportFactory = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<uint, PendingProbe> _pending = new();
    private readonly List<Task> _probeTasks = new();
    private readonly PingStats _stats = new();
    private readonly object _sendLock = new();
    private readonly TaskCompletionSource<IAckRawServerEndpoint> _connected =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<StopReason?> _stopped =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ITransport? _transport;
    private IAckRawServerEndpoint? _endpoint;
    private CancellationTokenSource? _statusCts;
    private Task? _statusTask;
    private ulong _sessionId;
    private long _connectStartedTicks;
    private long _connectedAtTicks;
    private long _protocolErrors;
    private StopReason? _lastStopReason;
    private volatile bool _shutdownRequested;
    private volatile bool _finished;
    private volatile bool _connectionLost;
    private string? _connectionError;

    public PingClient(PingOptions options, ILogger logger, PingLog log)
    {
        _options = options;
        _logger = logger;
        _log = log;
        _transportFactory.Register(new AckRawTcpClientProducer());
    }

    public ulong ClientId => _clientId;

    public string ClientIdHex => PingProtocol.FormatId(_clientId);

    public string SessionIdHex => PingProtocol.FormatId(_sessionId);

    public async Task<int> RunAsync()
    {
        _log.Event(
            "startup",
            ("protocol", PingProtocol.Version),
            ("client", ClientIdHex),
            ("url", _options.Url),
            ("interval_ms", _options.IntervalMs),
            ("size", _options.PayloadSize),
            ("wait_ms", _options.WaitMs),
            ("count", _options.Count),
            ("status_ms", _options.StatusMs));

        ITransport? transport;
        try
        {
            transport = _transportFactory.Construct(_options.Url, _logger, MemoryRental.Shared);
        }
        catch (Exception ex)
        {
            _log.Error("connect_fail", ("client", ClientIdHex), ("detail", ex.Message));
            return 1;
        }

        if (transport is not IAckRawClient client)
        {
            _log.Error("connect_fail", ("client", ClientIdHex), ("detail", "transport is not an acknowledged raw client"));
            return 1;
        }

        _log.Event("connect_begin", ("client", ClientIdHex), ("remote", _options.Url));

        client.Init(new PingClientHandler(this));
        _transport = transport;
        _connectStartedTicks = Stopwatch.GetTimestamp();
        if (!client.Start(OnTransportStopped))
        {
            _log.Error(
                "connect_fail",
                ("client", ClientIdHex),
                ("elapsed_ms", ElapsedMs(_connectStartedTicks)),
                ("detail", "transport failed to start"));
            return 1;
        }

        Task first = await Task.WhenAny(_connected.Task, _stopped.Task);
        if (first != _connected.Task)
        {
            StopReason? reason = await _stopped.Task;
            var fields = new List<(string Key, object? Value)>
            {
                ("client", ClientIdHex),
                ("elapsed_ms", ElapsedMs(_connectStartedTicks)),
                ("detail", _connectionError ?? "-")
            };
            if (reason != null)
            {
                fields.AddRange(PingLog.ReasonFields(reason));
            }

            _log.Error("connect_fail", fields.ToArray());
            _transport.Stop(StopReason.UserIntention);
            return 1;
        }

        _endpoint = await _connected.Task;
        _connectedAtTicks = Stopwatch.GetTimestamp();

        _log.Event(
            "connected",
            ("session", SessionIdHex),
            ("client", ClientIdHex),
            ("remote", _endpoint.RemoteEndPoint),
            ("connect_ms", ElapsedMs(_connectStartedTicks)),
            ("protocol", PingProtocol.Version));

        if (_options.StatusMs > 0)
        {
            _statusCts = new CancellationTokenSource();
            _statusTask = StatusLoopAsync(_statusCts.Token);
        }

        int exitCode = 0;

        try
        {
            await RunProbesAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_statusCts != null)
            {
                _statusCts.Cancel();
                if (_statusTask != null)
                {
                    try
                    {
                        await _statusTask;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }

            PingSummary summary = _stats.Snapshot();
            WriteStatusLine(summary, "final");

            _log.Raw("--- PigeonPost.Ping statistics ---");
            _log.Raw(string.Format(
                CultureInfo.InvariantCulture,
                "{0} packets transmitted, {1} received, {2} timeouts, {3} corrupted, {4:F0}% packet loss",
                summary.Transmitted,
                summary.Received,
                summary.Timeouts,
                summary.Corrupted,
                summary.LossPercent));
            if (summary.Received > 0)
            {
                _log.Raw(string.Format(
                    CultureInfo.InvariantCulture,
                    "rtt min/avg/max = {0:F2}/{1:F2}/{2:F2} ms",
                    summary.MinRtt,
                    summary.AverageRtt,
                    summary.MaxRtt));
            }

            LogVerdict(summary);

            _finished = true;
            _transport.Stop(StopReason.UserIntention);
            _cts.Cancel();

            if (!_shutdownRequested &&
                (_connectionLost ||
                 _protocolErrors > 0 ||
                 summary.Received != summary.Transmitted ||
                 summary.Corrupted > 0))
            {
                exitCode = 2;
            }
        }

        return exitCode;
    }

    public void RequestShutdown()
    {
        if (_shutdownRequested)
        {
            return;
        }

        _shutdownRequested = true;
        _cts.Cancel();
        _transport?.Stop(StopReason.UserIntention);
        _stopped.TrySetResult(null);
    }

    public void OnConnected(IAckRawServerEndpoint endpoint, ulong sessionId)
    {
        _sessionId = sessionId;
        _endpoint = endpoint;
        _connected.TrySetResult(endpoint);
    }

    public void OnHandshakeRejected(string reason, int serverVersion)
    {
        _connectionError = reason;
        _log.Error(
            "handshake_reject",
            ("client", ClientIdHex),
            ("reason", reason),
            ("server_protocol", serverVersion),
            ("protocol", PingProtocol.Version));
        _stopped.TrySetResult(null);
        _cts.Cancel();
    }

    public void OnReply(byte[] frame)
    {
        if (_finished || _shutdownRequested)
        {
            return;
        }

        if (!PingProtocol.TryDecodeFrame(frame, out uint sequence, out _, out _))
        {
            _stats.RecordCorrupted();
            _log.Event(
                "corrupt",
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("kind", "malformed_frame"),
                ("length", frame.Length));
            return;
        }

        if (_pending.TryGetValue(sequence, out PendingProbe? probe))
        {
            probe.Reply.TrySetResult(frame);
        }
        else
        {
            Interlocked.Increment(ref _protocolErrors);
            _log.Event(
                "protocol_error",
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("kind", "unexpected_reply"),
                ("seq", sequence));
        }
    }

    private void OnTransportStopped(StopReason reason)
    {
        _lastStopReason = reason;

        if (!_shutdownRequested && !_finished && _connected.Task.IsCompleted)
        {
            _connectionLost = true;
            PingSummary summary = _stats.Snapshot();
            var fields = new List<(string Key, object? Value)>
            {
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("duration_ms", ElapsedMs(_connectedAtTicks)),
                ("sent", summary.Transmitted),
                ("received", summary.Received),
                ("timeouts", summary.Timeouts),
                ("corrupt", summary.Corrupted)
            };
            fields.AddRange(PingLog.ReasonFields(reason));
            _log.Warn("transport_stop", fields.ToArray());
        }

        _stopped.TrySetResult(reason);
        _cts.Cancel();
    }

    private async Task RunProbesAsync(CancellationToken token)
    {
        uint sequence = 0;

        while (!token.IsCancellationRequested)
        {
            if (_options.Count > 0 && sequence >= (uint)_options.Count)
            {
                break;
            }

            _probeTasks.RemoveAll(static task => task.IsCompleted);
            _probeTasks.Add(ProbeAsync(sequence, token));
            sequence++;

            try
            {
                await Task.Delay(_options.IntervalMs, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        await Task.WhenAll(_probeTasks);
    }

    private async Task ProbeAsync(uint sequence, CancellationToken token)
    {
        byte[] payload = new byte[_options.PayloadSize];
        Random.Shared.NextBytes(payload);

        long timestamp = Stopwatch.GetTimestamp();
        byte[] frame = PingProtocol.EncodeFrame(sequence, timestamp, payload);

        var probe = new PendingProbe();
        _pending[sequence] = probe;
        _stats.RecordSent();

        SendResult result = SendFrame(frame);
        if (result != SendResult.Ok)
        {
            _pending.TryRemove(sequence, out _);
            _stats.RecordTimeout();
            _log.Warn(
                "probe_send_failed",
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("seq", sequence),
                ("result", result.ToString()));
            return;
        }

        if (_log.Verbose)
        {
            _log.Event(
                "probe_tx",
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("seq", sequence),
                ("bytes", frame.Length));
        }

        Task timeoutTask = Task.Delay(_options.WaitMs, token);
        Task completed = await Task.WhenAny(probe.Reply.Task, timeoutTask);

        if (completed == probe.Reply.Task)
        {
            byte[] replyFrame = await probe.Reply.Task;
            _pending.TryRemove(sequence, out _);
            HandleReply(sequence, timestamp, payload, replyFrame);
            return;
        }

        _pending.TryRemove(sequence, out _);
        if (!token.IsCancellationRequested)
        {
            _stats.RecordTimeout();
            PingSummary summary = _stats.Snapshot();
            _log.Event(
                "probe_timeout",
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("seq", sequence),
                ("waited_ms", _options.WaitMs),
                ("sent", summary.Transmitted),
                ("received", summary.Received));
        }
    }

    private SendResult SendFrame(byte[] frame)
    {
        lock (_sendLock)
        {
            IAckRawServerEndpoint? endpoint = _endpoint;
            if (endpoint == null || !endpoint.IsConnected)
            {
                return SendResult.NotConnected;
            }

            try
            {
                var message = new UnionDataList();
                message.PutFirst(new UnionData(new StaticReadOnlyByteArray(frame)));
                return endpoint.Send(message);
            }
            catch (Exception)
            {
                return SendResult.Error;
            }
        }
    }

    private void HandleReply(uint sequence, long sentTimestamp, byte[] payload, byte[] replyFrame)
    {
        if (!PingProtocol.TryDecodeFrame(replyFrame, out uint replySequence, out long replyTimestamp,
                out ReadOnlySpan<byte> replyPayload))
        {
            _stats.RecordCorrupted();
            _log.Event(
                "corrupt",
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("seq", sequence),
                ("kind", "malformed_frame"),
                ("length", replyFrame.Length));
            return;
        }

        if (replySequence != sequence)
        {
            _stats.RecordCorrupted();
            _log.Event(
                "corrupt",
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("seq", sequence),
                ("kind", "sequence_mismatch"),
                ("actual", replySequence));
            return;
        }

        if (replyTimestamp != sentTimestamp)
        {
            _stats.RecordCorrupted();
            _log.Event(
                "corrupt",
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("seq", sequence),
                ("kind", "timestamp_mismatch"));
            return;
        }

        if (!PingProtocol.IsReversedCopy(payload, replyPayload))
        {
            _stats.RecordCorrupted();
            _log.Event(
                "corrupt",
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("seq", sequence),
                ("kind", "payload_mismatch"));
            return;
        }

        double rtt = Stopwatch.GetElapsedTime(sentTimestamp).TotalMilliseconds;
        _stats.RecordReply(rtt);
        _log.Event(
            "reply_rx",
            ("session", SessionIdHex),
            ("client", ClientIdHex),
            ("seq", sequence),
            ("bytes", replyPayload.Length),
            ("rtt_ms", rtt));
    }

    private async Task StatusLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.StatusMs));
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                WriteStatusLine(_stats.Snapshot(), "periodic");
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void WriteStatusLine(PingSummary summary, string phase)
    {
        string status = _connectionLost
            ? "terminated"
            : summary.Corrupted > 0 || Interlocked.Read(ref _protocolErrors) > 0
                ? "degraded"
                : summary.Transmitted == 0
                    ? "waiting"
                    : summary.Received == summary.Transmitted
                        ? "ok"
                        : "degraded";

        _log.Event(
            "status",
            ("session", SessionIdHex),
            ("client", ClientIdHex),
            ("phase", phase),
            ("status", status),
            ("sent", summary.Transmitted),
            ("received", summary.Received),
            ("timeouts", summary.Timeouts),
            ("corrupt", summary.Corrupted),
            ("protocol_errors", Interlocked.Read(ref _protocolErrors)),
            ("last_reply_age_ms", summary.LastReplyAgeMs),
            ("uptime_ms", ElapsedMs(_connectedAtTicks)));
    }

    private void LogVerdict(PingSummary summary)
    {
        string result;
        string detail;
        long protocolErrors = Interlocked.Read(ref _protocolErrors);

        if (summary.Corrupted > 0 || protocolErrors > 0)
        {
            result = summary.Corrupted > 0 ? "CORRUPTION" : "PROTOCOL_ERROR";
            detail = summary.Corrupted > 0
                ? "received replies failed verification"
                : "protocol-level inconsistencies were observed";
        }
        else if (_connectionLost)
        {
            result = "TERMINATED";
            StopReason? cause = _lastStopReason == null ? null : PingLog.Unwrap(_lastStopReason);
            detail = $"transport stopped: {cause?.Type ?? "unknown"}";
        }
        else if (summary.Transmitted == 0)
        {
            result = "NO_PROBES";
            detail = "no probes were sent";
        }
        else if (summary.Received != summary.Transmitted)
        {
            result = _shutdownRequested ? "INTERRUPTED" : "INCOMPLETE";
            detail = summary.Received == 0
                ? "sends succeeded but no replies were received"
                : "some probes did not receive a verified reply";
        }
        else
        {
            result = "OK";
            detail = "all probes were replied to and verified";
        }

        _log.Event(
            "verdict",
            ("session", SessionIdHex),
            ("client", ClientIdHex),
            ("result", result),
            ("detail", detail));
    }

    private static long ElapsedMs(long startTicks)
    {
        return startTicks == 0
            ? 0
            : (long)Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
    }

    private sealed class PendingProbe
    {
        public TaskCompletionSource<byte[]> Reply { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

internal sealed class PingClientHandler : IAckRawClientHandler
{
    private readonly PingClient _client;
    private readonly byte[] _handshake;

    public PingClientHandler(PingClient client)
    {
        _client = client;
        _handshake = PingProtocol.CreateHandshake(client.ClientId);
    }

    public void WriteAckData(UnionDataList ackData)
    {
        ackData.PutFirst(new UnionData(new StaticReadOnlyByteArray(_handshake)));
    }

    public void OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)
    {
        using var disposer = ackResponse.AsDisposable();

        byte[]? ackBytes = null;
        if (ackResponse.TryPopFirst(out IMultiRefReadOnlyByteArray? ackData))
        {
            ackBytes = ackData.ToArray();
            ackData.Release();
        }

        if (ackBytes == null)
        {
            _client.OnHandshakeRejected("missing_payload", -1);
            endPoint.Disconnect(StopReason.UserIntention);
            return;
        }

        if (!PingProtocol.TryDecodeHandshakeAck(ackBytes, out ulong sessionId))
        {
            _client.OnHandshakeRejected(
                PingProtocol.DescribeTokenFailure(ackBytes),
                PingProtocol.GetTokenVersion(ackBytes));
            endPoint.Disconnect(StopReason.UserIntention);
            return;
        }

        _client.OnConnected(endPoint, sessionId);
    }

    public void OnReceived(UnionDataList receivedBuffer)
    {
        using var guard = receivedBuffer.AsDisposable();

        if (receivedBuffer.TryPopFirst(out IMultiRefReadOnlyByteArray? data))
        {
            byte[]? frame = data.ToArray();
            data.Release();

            if (frame != null)
            {
                _client.OnReply(frame);
            }
        }
    }

    public void OnDisconnected(StopReason reason)
    {
    }

    public void OnStopped(StopReason reason)
    {
    }
}
