using System;
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
    private readonly string _url;
    private readonly ILogger _logger;
    private readonly TransportFactory _transportFactory = new();
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _consoleLock = new();

    private ITransport? _transport;
    private int _activeClients;
    private int _stopping;

    public PingServer(string url, ILogger logger)
    {
        _url = url;
        _logger = logger;
        _transportFactory.Register(new AckRawTcpServerProducer());
    }

    public Task RunAsync()
    {
        ITransport? transport = _transportFactory.Construct(_url, _logger, MemoryRental.Shared);
        if (transport is not IAckRawServer server)
        {
            throw new InvalidOperationException("Transport is not an acknowledged raw server.");
        }

        server.Init(new PingServerAcknowledger(this, _logger));
        _transport = transport;

        WriteLine($"PigeonPost.Ping server listening on {_url}");
        server.Start(OnTransportStopped);

        return _stopped.Task;
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopping, 1) != 0)
        {
            return;
        }

        _transport?.Stop(StopReason.UserIntention);
        _stopped.TrySetResult();
    }

    public void OnClientConnected(IAckRawClientEndpoint endpoint)
    {
        int active = Interlocked.Increment(ref _activeClients);
        WriteLine($"Client connected: {FormatEndPoint(endpoint)} (active: {active})");
    }

    public void OnClientDisconnected(StopReason reason)
    {
        int active = Interlocked.Decrement(ref _activeClients);
        WriteLine($"Client disconnected ({reason.Type}) (active: {Math.Max(0, active)})");
    }

    private void OnTransportStopped(StopReason reason)
    {
        if (Volatile.Read(ref _stopping) == 0)
        {
            WriteLine($"Server transport stopped: {reason.Type}");
        }

        _stopped.TrySetResult();
    }

    private void WriteLine(string message)
    {
        lock (_consoleLock)
        {
            Console.Out.WriteLine(message);
        }
    }

    private static string FormatEndPoint(IAckRawClientEndpoint endpoint)
    {
        return endpoint.RemoteEndPoint?.ToString() ?? "unknown";
    }
}

internal sealed class PingServerAcknowledger : IRawServerAcknowledger<PingServerHandler>
{
    private readonly PingServer _server;
    private readonly ILogger _logger;

    public PingServerAcknowledger(PingServer server, ILogger logger)
    {
        _server = server;
        _logger = logger;
    }

    public PingServerHandler? TryAck(UnionDataList ackData)
    {
        using var disposer = ackData.AsDisposable();

        if (ackData.TryPopFirst(out IMultiRefReadOnlyByteArray? data))
        {
            byte[]? handshake = data.ToArray();
            data.Release();

            if (handshake != null && PingProtocol.IsValidHandshake(handshake))
            {
                return new PingServerHandler(_server, _logger);
            }
        }

        _logger.w("Rejected client: invalid PigeonPost.Ping handshake.");
        return null;
    }
}

internal sealed class PingServerHandler : IAckRawServerHandler
{
    private readonly PingServer _server;
    private readonly ILogger _logger;
    private volatile IAckRawClientEndpoint? _endpoint;

    public PingServerHandler(PingServer server, ILogger logger)
    {
        _server = server;
        _logger = logger;
    }

    public void OnConnected(IAckRawClientEndpoint endPoint)
    {
        _endpoint = endPoint;
        _server.OnClientConnected(endPoint);
    }

    public void GetAckResponse(UnionDataList ackData)
    {
        byte[] handshake = PingProtocol.CreateHandshake();
        ackData.PutFirst(new UnionData(new StaticReadOnlyByteArray(handshake)));
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

        if (!PingProtocol.TryDecodeFrame(frame, out uint sequence, out _, out _))
        {
            _logger.w($"Dropped malformed frame ({frame.Length} bytes).");
            return;
        }

        IAckRawClientEndpoint? endpoint = _endpoint;
        if (endpoint == null || !endpoint.IsConnected)
        {
            return;
        }

        byte[] replyBytes = PingProtocol.CreateReversedReply(frame);
        var message = new UnionDataList();
        message.PutFirst(new UnionData(new StaticReadOnlyByteArray(replyBytes)));

        SendResult result = endpoint.Send(message);
        if (result != SendResult.Ok)
        {
            _logger.w($"Failed to echo seq={sequence}: {result}");
        }
    }

    public void OnDisconnected(StopReason reason)
    {
        _server.OnClientDisconnected(reason);
    }
}
