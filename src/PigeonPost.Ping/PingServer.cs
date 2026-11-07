using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Abstractions.Servers;
using Pontifex.Transports.Tcp;
using Pontifex.Utils;
using Scriba;

namespace PigeonPost.Ping;

internal sealed class PingServer
{
    private readonly PingOptions _options;
    private readonly string _url;
    private readonly ILogger _logger;
    private readonly PingLog _log;
    private readonly TransportFactory _transportFactory = new();
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentDictionary<ulong, PingServerHandler> _sessions = new();

    private ITransport? _transport;
    private CancellationTokenSource? _statusCts;
    private Task? _statusTask;
    private int _stopping;

    public PingServer(PingOptions options, ILogger logger, PingLog log)
    {
        _options = options;
        _url = options.Url;
        _logger = logger;
        _log = log;
        _transportFactory.Register(new AckRawTcpServerProducer());
    }

    public async Task RunAsync()
    {
        _log.Event(
            "startup",
            ("protocol", PingProtocol.Version),
            ("url", _url),
            ("status_ms", _options.StatusMs));

        ITransport? transport = _transportFactory.Construct(_url, _logger, MemoryRental.Shared);
        if (transport is not IAckRawServer server)
        {
            throw new InvalidOperationException("Transport is not an acknowledged raw server.");
        }

        server.Init(new PingServerAcknowledger(this, _log));
        _transport = transport;

        _log.Event("listening", ("url", _url), ("protocol", PingProtocol.Version));
        if (!server.Start(OnTransportStopped))
        {
            throw new InvalidOperationException("Failed to start the TCP server transport.");
        }

        if (_options.StatusMs > 0)
        {
            _statusCts = new CancellationTokenSource();
            _statusTask = StatusLoopAsync(_statusCts.Token);
        }

        await _stopped.Task;

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
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopping, 1) != 0)
        {
            return;
        }

        _statusCts?.Cancel();
        _transport?.Stop(StopReason.UserIntention);
        _stopped.TrySetResult();
    }

    public void OnClientConnected(PingServerHandler handler)
    {
        _sessions[handler.SessionId] = handler;
        _log.Event(
            "session_open",
            ("session", handler.SessionIdHex),
            ("client", handler.ClientIdHex),
            ("remote", handler.Remote),
            ("protocol", PingProtocol.Version));
    }

    public void OnClientDisconnected(PingServerHandler handler, StopReason reason)
    {
        _sessions.TryRemove(handler.SessionId, out _);

        PingSessionSnapshot snapshot = handler.Snapshot();
        string verdict = snapshot.ProtocolErrors > 0
            ? "PROTOCOL_ERROR"
            : snapshot.FramesIn == 0
                ? "NO_INBOUND"
                : snapshot.FramesEchoed < snapshot.FramesIn
                    ? "ECHO_FAILURE"
                    : "HEALTHY";

        var fields = new List<(string Key, object? Value)>
        {
            ("session", handler.SessionIdHex),
            ("client", handler.ClientIdHex),
            ("remote", handler.Remote),
            ("duration_ms", snapshot.DurationMs),
            ("frames_rx", snapshot.FramesIn),
            ("echoes", snapshot.FramesEchoed),
            ("bytes_rx", snapshot.BytesIn),
            ("bytes_tx", snapshot.BytesOut),
            ("protocol_errors", snapshot.ProtocolErrors),
            ("inbound_age_ms", snapshot.InboundAgeMs),
            ("verdict", verdict)
        };
        fields.AddRange(PingLog.ReasonFields(reason));

        _log.Event("session_close", fields.ToArray());
    }

    private async Task StatusLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.StatusMs));
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                _log.Event("status", ("active", _sessions.Count));
                foreach (PingServerHandler handler in _sessions.Values)
                {
                    handler.WriteStatus();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnTransportStopped(StopReason reason)
    {
        _statusCts?.Cancel();

        if (Volatile.Read(ref _stopping) == 0)
        {
            _log.Event("transport_stop", PingLog.ReasonFields(reason));
        }

        _stopped.TrySetResult();
    }
}

internal sealed class PingServerAcknowledger : IRawServerAcknowledger<PingServerHandler>
{
    private readonly PingServer _server;
    private readonly PingLog _log;

    public PingServerAcknowledger(PingServer server, PingLog log)
    {
        _server = server;
        _log = log;
    }

    public PingServerHandler? TryAck(UnionDataList ackData)
    {
        using var disposer = ackData.AsDisposable();

        if (!ackData.TryPopFirst(out IMultiRefReadOnlyByteArray? data))
        {
            _log.Warn(
                "handshake_reject",
                ("reason", "missing_payload"),
                ("protocol", PingProtocol.Version));
            return null;
        }

        byte[]? handshake = data.ToArray();
        data.Release();

        if (handshake == null)
        {
            _log.Warn(
                "handshake_reject",
                ("reason", "missing_payload"),
                ("protocol", PingProtocol.Version));
            return null;
        }

        if (!PingProtocol.TryDecodeHandshake(handshake, out ulong clientId))
        {
            _log.Warn(
                "handshake_reject",
                ("reason", PingProtocol.DescribeTokenFailure(handshake)),
                ("length", handshake.Length),
                ("client_protocol", PingProtocol.GetTokenVersion(handshake)),
                ("protocol", PingProtocol.Version));
            return null;
        }

        ulong sessionId = PingProtocol.CreateId();
        _log.Event(
            "handshake_ok",
            ("session", PingProtocol.FormatId(sessionId)),
            ("client", PingProtocol.FormatId(clientId)),
            ("protocol", PingProtocol.Version));

        return new PingServerHandler(_server, _log, sessionId, clientId);
    }
}

internal sealed class PingServerHandler : IAckRawServerHandler
{
    private readonly PingServer _server;
    private readonly PingLog _log;
    private readonly ulong _sessionId;
    private readonly ulong _clientId;

    private IAckRawClientEndpoint? _endpoint;
    private string _remote = "unknown";
    private long _startedTicks;
    private long _lastInboundTicks;
    private long _framesIn;
    private long _framesEchoed;
    private long _bytesIn;
    private long _bytesOut;
    private long _protocolErrors;
    private uint _expectedSequence;
    private bool _hasFrame;

    public PingServerHandler(PingServer server, PingLog log, ulong sessionId, ulong clientId)
    {
        _server = server;
        _log = log;
        _sessionId = sessionId;
        _clientId = clientId;
    }

    public ulong SessionId => _sessionId;

    public string SessionIdHex => PingProtocol.FormatId(_sessionId);

    public string ClientIdHex => PingProtocol.FormatId(_clientId);

    public string Remote => _remote;

    public void OnConnected(IAckRawClientEndpoint endPoint)
    {
        _endpoint = endPoint;
        _remote = endPoint.RemoteEndPoint?.ToString() ?? "unknown";
        _startedTicks = Stopwatch.GetTimestamp();
        _lastInboundTicks = _startedTicks;
        _server.OnClientConnected(this);
    }

    public void GetAckResponse(UnionDataList ackData)
    {
        byte[] ack = PingProtocol.CreateHandshakeAck(_sessionId);
        ackData.PutFirst(new UnionData(new StaticReadOnlyByteArray(ack)));
    }

    public void OnReceived(UnionDataList receivedBuffer)
    {
        using var guard = receivedBuffer.AsDisposable();

        if (!receivedBuffer.TryPopFirst(out IMultiRefReadOnlyByteArray? data))
        {
            return;
        }

        byte[]? frame = data.ToArray();
        data.Release();

        if (frame == null)
        {
            return;
        }

        _lastInboundTicks = Stopwatch.GetTimestamp();

        if (!PingProtocol.TryDecodeFrame(frame, out uint sequence, out long timestamp, out _))
        {
            _protocolErrors++;
            _log.Warn(
                "frame_reject",
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("reason", "malformed_frame"),
                ("length", frame.Length));
            return;
        }

        CheckSequence(sequence);

        _framesIn++;
        _bytesIn += frame.Length;

        if (_log.Verbose)
        {
            _log.Event(
                "frame_rx",
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("seq", sequence),
                ("bytes", frame.Length),
                ("timestamp", timestamp));
        }

        IAckRawClientEndpoint? endpoint = _endpoint;
        if (endpoint == null || !endpoint.IsConnected)
        {
            _log.Warn(
                "echo_failed",
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("seq", sequence),
                ("reason", "not_connected"));
            return;
        }

        byte[] replyBytes = PingProtocol.CreateReversedReply(frame);
        var message = new UnionDataList();
        message.PutFirst(new UnionData(new StaticReadOnlyByteArray(replyBytes)));

        SendResult result = endpoint.Send(message);
        if (result != SendResult.Ok)
        {
            _log.Warn(
                "echo_failed",
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("seq", sequence),
                ("result", result.ToString()));
            return;
        }

        _framesEchoed++;
        _bytesOut += replyBytes.Length;

        if (_log.Verbose)
        {
            _log.Event(
                "echo_tx",
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("seq", sequence),
                ("bytes", replyBytes.Length));
        }
    }

    public void OnDisconnected(StopReason reason)
    {
        _server.OnClientDisconnected(this, reason);
    }

    public void WriteStatus()
    {
        PingSessionSnapshot snapshot = Snapshot();
        string status = snapshot.ProtocolErrors > 0
            ? "degraded"
            : snapshot.FramesIn > 0
                ? "ok"
                : "waiting";

        _log.Event(
            "status",
            ("session", SessionIdHex),
            ("client", ClientIdHex),
            ("remote", _remote),
            ("frames_rx", snapshot.FramesIn),
            ("echoes", snapshot.FramesEchoed),
            ("bytes_rx", snapshot.BytesIn),
            ("bytes_tx", snapshot.BytesOut),
            ("protocol_errors", snapshot.ProtocolErrors),
            ("inbound_age_ms", snapshot.InboundAgeMs),
            ("status", status));
    }

    public PingSessionSnapshot Snapshot()
    {
        long now = Stopwatch.GetTimestamp();
        long durationMs = _startedTicks == 0
            ? 0
            : (long)Stopwatch.GetElapsedTime(_startedTicks, now).TotalMilliseconds;
        double inboundAgeMs = _lastInboundTicks == 0
            ? -1
            : Stopwatch.GetElapsedTime(_lastInboundTicks, now).TotalMilliseconds;

        return new PingSessionSnapshot(
            durationMs,
            _framesIn,
            _framesEchoed,
            _bytesIn,
            _bytesOut,
            _protocolErrors,
            inboundAgeMs);
    }

    private void CheckSequence(uint sequence)
    {
        if (!_hasFrame)
        {
            _hasFrame = true;
            if (sequence != 0)
            {
                _protocolErrors++;
                _log.Warn(
                    "protocol_error",
                    ("session", SessionIdHex),
                    ("client", ClientIdHex),
                    ("kind", "first_sequence"),
                    ("expected", 0),
                    ("actual", sequence));
            }
        }
        else if (sequence != _expectedSequence)
        {
            _protocolErrors++;
            _log.Warn(
                "protocol_error",
                ("session", SessionIdHex),
                ("client", ClientIdHex),
                ("kind", "sequence_gap"),
                ("expected", _expectedSequence),
                ("actual", sequence));
        }

        _expectedSequence = sequence + 1;
    }
}

internal readonly record struct PingSessionSnapshot(
    long DurationMs,
    long FramesIn,
    long FramesEchoed,
    long BytesIn,
    long BytesOut,
    long ProtocolErrors,
    double InboundAgeMs);
