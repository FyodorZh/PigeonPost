using System;
using System.IO;
using System.Threading;
using Pontifex;
using Pontifex.Abstractions.Endpoints;
using Pontifex.StopReasons;
using PigeonPost.Tun;
using Scriba;

namespace PigeonPost.Bridge;

public sealed class BridgeImpl : IBridge, IDisposable
{
    private readonly ITunDevice _tun;
    private readonly IPacketBuffer _buffer;
    private readonly ILogger _logger;
    private readonly bool _verbose;

    private volatile bool _running;
    private Thread? _tunReaderThread;
    private IAckRawBaseEndpoint? _endpoint;
    private readonly object _endpointLock = new();
    private Action<byte[]>? _packetHandler;

    private long _packetsIn;
    private long _packetsOut;
    private long _bytesIn;
    private long _bytesOut;
    private long _droppedPackets;

    public event Action<StopReason>? OnStopped;
    public event Action<IAckRawBaseEndpoint>? EndpointConnected;

    public BridgeImpl(ITunDevice tun, IPacketBuffer buffer, ILogger logger, bool verbose = false)
    {
        _tun = tun ?? throw new ArgumentNullException(nameof(tun));
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _verbose = verbose;
    }

    public void SetPacketHandler(Action<byte[]>? handler)
    {
        _packetHandler = handler;
    }

    public void Start()
    {
        if (_running)
            throw new InvalidOperationException("Bridge already started.");

        _running = true;
        _tunReaderThread = new Thread(TunReaderLoop)
        {
            Name = "PigeonPost-TunReader",
            IsBackground = true
        };
        _tunReaderThread.Start();
    }

    public void Stop(StopReason reason)
    {
        _running = false;

        _tunReaderThread?.Join(TimeSpan.FromSeconds(5));

        OnEndpointDisconnected();
        OnStopped?.Invoke(reason);
    }

    private void TunReaderLoop()
    {
        byte[] readBuffer = new byte[65536];

        while (_running)
        {
            try
            {
                int bytesRead = _tun.Read(readBuffer);
                if (bytesRead <= 0) continue;

                byte[] packet = new byte[bytesRead];
                Array.Copy(readBuffer, packet, bytesRead);

                Interlocked.Add(ref _bytesIn, bytesRead);
                Interlocked.Increment(ref _packetsIn);

                if (_verbose)
                    _logger.i($"TUN ← {bytesRead} bytes (#{_packetsIn})");

                var handler = _packetHandler;
                if (handler != null)
                {
                    handler(packet);
                }
                else if (TryGetEndpoint(out var endpoint))
                {
                    SendPacket(endpoint, packet);
                }
                else
                {
                    if (!_buffer.TryEnqueue(packet))
                    {
                        Interlocked.Increment(ref _droppedPackets);
                        if (_verbose)
                            _logger.w($"Buffer full, dropped packet ({bytesRead} bytes). Dropped total: {_droppedPackets}");
                    }
                }
            }
            catch (IOException ex)
            {
                _logger.e($"TUN read error: {ex.Message}");
                Stop(new ExceptionFail("bridge", ex, ex.Message));
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    public void OnEndpointConnected(IAckRawBaseEndpoint endpoint)
    {
        lock (_endpointLock)
        {
            _endpoint = endpoint;
        }

        _logger.i("Pontifex endpoint connected.");
        EndpointConnected?.Invoke(endpoint);

        while (_running && _buffer.TryDequeue(out byte[] packet))
        {
            if (TryGetEndpoint(out var ep))
                SendPacket(ep, packet);
            else
                break;
        }
    }

    public void OnEndpointDisconnected()
    {
        lock (_endpointLock)
        {
            _endpoint = null;
        }

        _logger.i("Pontifex endpoint disconnected.");
    }

    public void OnPacketReceived(byte[] packet)
    {
        Interlocked.Add(ref _bytesOut, packet.Length);
        Interlocked.Increment(ref _packetsOut);

        if (_verbose)
            _logger.i($"TUN → {packet.Length} bytes (out #{_packetsOut})");

        try
        {
            _tun.Write(packet);
        }
        catch (IOException ex)
        {
            _logger.e($"TUN write error: {ex.Message}");
            Stop(new ExceptionFail("bridge", ex, ex.Message));
        }
    }

    public bool TryGetNextPacket(out byte[] packet)
    {
        return _buffer.TryDequeue(out packet!);
    }

    public void OnTransportStopped(StopReason reason)
    {
        _logger.i($"Transport stopped: {reason.Type}");

        OnEndpointDisconnected();

        OnStopped?.Invoke(reason);
    }

    private bool TryGetEndpoint(out IAckRawBaseEndpoint endpoint)
    {
        lock (_endpointLock)
        {
            endpoint = _endpoint!;
            return _endpoint != null;
        }
    }

    private void SendPacket(IAckRawBaseEndpoint endpoint, byte[] packet)
    {
        var message = PontifexPacketConverter.CreateMessage(packet);
        var result = endpoint.Send(message);

        if (result != SendResult.Ok && _verbose)
            _logger.w($"Send failed: {result}");
    }

    public void Dispose()
    {
        _running = false;
        _tunReaderThread?.Join(TimeSpan.FromSeconds(3));
    }
}
