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
    private readonly TransportFactory _transportFactory = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<uint, PendingProbe> _pending = new();
    private readonly List<Task> _probeTasks = new();
    private readonly PingStats _stats = new();
    private readonly object _sendLock = new();
    private readonly object _consoleLock = new();
    private readonly TaskCompletionSource<IAckRawServerEndpoint> _connected =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<StopReason?> _stopped =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ITransport? _transport;
    private IAckRawServerEndpoint? _endpoint;
    private volatile bool _shutdownRequested;
    private volatile bool _finished;
    private volatile bool _connectionLost;
    private string? _connectionError;

    public PingClient(PingOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
        _transportFactory.Register(new AckRawTcpClientProducer());
    }

    public async Task<int> RunAsync()
    {
        WriteLine($"PING {_options.Url}: {_options.PayloadSize} data bytes, interval={_options.IntervalMs} ms, timeout={_options.WaitMs} ms");

        ITransport? transport;
        try
        {
            transport = _transportFactory.Construct(_options.Url, _logger, MemoryRental.Shared);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: failed to create transport: {ex.Message}");
            return 1;
        }

        if (transport is not IAckRawClient client)
        {
            Console.Error.WriteLine("Error: transport is not an acknowledged raw client.");
            return 1;
        }

        client.Init(new PingClientHandler(this));
        _transport = transport;
        client.Start(OnTransportStopped);

        Task first = await Task.WhenAny(_connected.Task, _stopped.Task);
        if (first != _connected.Task)
        {
            StopReason? reason = await _stopped.Task;
            Console.Error.WriteLine(_connectionError ?? $"Error: connection failed ({reason?.Type ?? "unknown"}).");
            _transport.Stop(StopReason.UserIntention);
            return 1;
        }

        _endpoint = await _connected.Task;
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
            PingSummary summary = _stats.Snapshot();
            WriteLine("--- PigeonPost.Ping statistics ---");
            WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0} packets transmitted, {1} received, {2} corrupted, {3:F0}% packet loss",
                summary.Transmitted,
                summary.Received,
                summary.Corrupted,
                summary.LossPercent));
            if (summary.Received > 0)
            {
                WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "rtt min/avg/max = {0:F2}/{1:F2}/{2:F2} ms",
                    summary.MinRtt,
                    summary.AverageRtt,
                    summary.MaxRtt));
            }

            _finished = true;
            _transport.Stop(StopReason.UserIntention);
            _cts.Cancel();

            if (!_shutdownRequested &&
                (_connectionLost || summary.Received != summary.Transmitted || summary.Corrupted > 0))
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

    public void OnConnected(IAckRawServerEndpoint endpoint)
    {
        _endpoint = endpoint;
        _connected.TrySetResult(endpoint);
    }

    public void OnHandshakeRejected(string message)
    {
        _connectionError = message;
        _stopped.TrySetResult(null);
        _cts.Cancel();
    }

    public void OnReply(byte[] frame)
    {
        if (!PingProtocol.TryDecodeFrame(frame, out uint sequence, out _, out _))
        {
            _stats.RecordCorrupted();
            WriteLine("Received a malformed reply.");
            return;
        }

        if (_pending.TryGetValue(sequence, out PendingProbe? probe))
        {
            probe.Reply.TrySetResult(frame);
        }
        else
        {
            WriteLine($"Received an unexpected reply for seq={sequence}");
        }
    }

    private void OnTransportStopped(StopReason reason)
    {
        if (!_shutdownRequested && !_finished && _connected.Task.IsCompleted)
        {
            _connectionLost = true;
            WriteError($"Connection lost: {reason.Type}");
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
            WriteLine($"Send failed for seq={sequence}: {result}");
            return;
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
            WriteLine($"Request timeout for seq={sequence}");
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
            WriteLine($"Corrupted reply for seq={sequence}: malformed frame");
            return;
        }

        if (replySequence != sequence)
        {
            _stats.RecordCorrupted();
            WriteLine($"Corrupted reply for seq={sequence}: sequence mismatch ({replySequence})");
            return;
        }

        if (replyTimestamp != sentTimestamp)
        {
            _stats.RecordCorrupted();
            WriteLine($"Corrupted reply for seq={sequence}: timestamp mismatch");
            return;
        }

        if (!PingProtocol.IsReversedCopy(payload, replyPayload))
        {
            _stats.RecordCorrupted();
            WriteLine($"Corrupted reply for seq={sequence}: payload mismatch");
            return;
        }

        double rtt = Stopwatch.GetElapsedTime(sentTimestamp).TotalMilliseconds;
        _stats.RecordReply(rtt);
        WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0} bytes from {1}: seq={2} time={3:F2} ms",
            replyPayload.Length,
            _endpoint?.RemoteEndPoint,
            sequence,
            rtt));
    }

    private void WriteLine(string message)
    {
        lock (_consoleLock)
        {
            Console.Out.WriteLine(message);
        }
    }

    private void WriteError(string message)
    {
        lock (_consoleLock)
        {
            Console.Error.WriteLine(message);
        }
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
    private readonly byte[] _handshake = PingProtocol.CreateHandshake();

    public PingClientHandler(PingClient client)
    {
        _client = client;
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

        if (ackBytes == null || !PingProtocol.IsValidHandshake(ackBytes))
        {
            _client.OnHandshakeRejected("Error: server rejected the PigeonPost.Ping handshake.");
            endPoint.Disconnect(StopReason.UserIntention);
            return;
        }

        _client.OnConnected(endPoint);
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
