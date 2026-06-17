using System.Threading.Tasks;
using Pontifex;
using Pontifex.Protocols.Monitoring.AckRaw;
using Pontifex.Transports.Direct;
using Pontifex.Transports.Tcp;
using Scriba;

namespace PigeonPost;

internal abstract class BaseApp
{
    protected readonly TransportFactory _serverTransportFactory = new();
    protected readonly TransportFactory _clientTransportFactory = new();
    
    protected readonly BridgeConfiguration _config;
    protected readonly ILogger _logger;

    protected BaseApp(BridgeConfiguration config, ILogger logger)
    {
        _config = config;
        _logger = logger;

        _serverTransportFactory.Register(new AckRawDirectServerProducer());
        _serverTransportFactory.Register(new AckRawLoggerServerProducer());
        _serverTransportFactory.Register(new AckRawTcpServerProducer());
        
        _clientTransportFactory.Register(new AckRawDirectClientProducer());
        _clientTransportFactory.Register(new AckRawLoggerClientProducer());
        _clientTransportFactory.Register(new AckRawTcpClientProducer());
    }

    public abstract Task RunAsync();

    public abstract void RequestShutdown();

    protected static string FormatIp(uint ip)
    {
        return $"{(ip >> 24) & 0xFF}.{(ip >> 16) & 0xFF}.{(ip >> 8) & 0xFF}.{ip & 0xFF}";
    }
}
