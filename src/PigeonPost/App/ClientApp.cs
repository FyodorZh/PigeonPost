using System;
using System.Threading.Tasks;
using PigeonPost.Bridge;
using PigeonPost.Tun;
using Scriba;

namespace PigeonPost;

internal sealed class ClientApp : BaseApp
{
    private readonly TunDevice _tun;
    private readonly ClientSideLogic _logic;
    
    public ClientApp(BridgeConfiguration config, ILogger logger) : base(config, logger)
    {
        if (config.Role != Role.Client)
            throw new ArgumentException("Role must be Client.", nameof(config));
        
        var tunName = _config.TunNames[0];
        var clientId = _config.ClientId!;

        uint hostIpv4;
        try
        {
            hostIpv4 = TunIpv4AddressResolver.ResolveIpv4Address(tunName);
        }
        catch (Exception ex)
        {
            _logger.e($"Failed to resolve TUN IPv4 address: {ex.Message}");
            throw;
        }

        _logger.i($"Client ID: {clientId}, host IPv4: {FormatIp(hostIpv4)}");

        _tun = new TunDevice(tunName);
        _tun.SetSendBufferSize(1048576);
        _logger.i($"TUN device '{tunName}' opened.");

        _logic = new ClientSideLogic(
            _tun,
            new ClientId(clientId),
            new IPv4(hostIpv4),
            new IPv4(0),
            _config.PontifexUrl,
            _logger,
            _clientTransportFactory,
            _config.BufferSizeBytes,
            _config.Verbose);
    }

    public override async Task RunAsync()
    {
        var stoppedTcs = new TaskCompletionSource();
        _logic.Stopped += _ => stoppedTcs.TrySetResult();

        _ = _logic.Start();

        await stoppedTcs.Task;

        _logic.Stop();
        _tun.Close();
        _logger.i("Client shut down.");
    }

    public override void RequestShutdown()
    {
        _logic.RequestShutdown();
    }
}
