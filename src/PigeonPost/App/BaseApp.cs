using System;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using Pontifex.Abstractions.Servers;
using Pontifex.Transports.Direct;
using Pontifex.Transports.Tcp;
using Scriba;

namespace PigeonPost;

internal abstract class BaseApp
{
    protected readonly BridgeConfiguration _config;
    protected readonly ILogger _logger;

    protected volatile bool _shutdownRequested;
    protected readonly CancellationTokenSource _cts = new();

    protected BaseApp(BridgeConfiguration config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public abstract Task RunAsync();

    public void RequestShutdown()
    {
        _shutdownRequested = true;
        _cts.Cancel();
    }

    protected ITransport CreateTransport(string url, bool isServer)
    {
        if (url.StartsWith("direct|"))
        {
            string name = url.Substring("direct|".Length);
            if (isServer)
                return new AckRawDirectServer(name, _logger, MemoryRental.Shared);
            else
                return new AckRawDirectClient(name, _logger, MemoryRental.Shared);
        }

        var factory = new TransportFactory();
        var reg = new TransportFactoryRegistrator(factory);

        if (isServer)
            reg.Register<AckRawTcpServerProducer>();
        else
            reg.Register<AckRawTcpClientProducer>();

        var transport = factory.Construct(url, _logger, MemoryRental.Shared);
        if (transport == null)
            throw new InvalidOperationException($"Failed to construct transport from URL: '{url}'");
        return transport;
    }

    protected async Task WaitForShutdownAsync()
    {
        try
        {
            await Task.Delay(Timeout.Infinite, _cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    protected static string FormatIp(uint ip)
    {
        return $"{(ip >> 24) & 0xFF}.{(ip >> 16) & 0xFF}.{(ip >> 8) & 0xFF}.{ip & 0xFF}";
    }
}
