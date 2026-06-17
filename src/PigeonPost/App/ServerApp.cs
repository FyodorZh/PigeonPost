using System;
using System.Threading.Tasks;
using PigeonPost.Tun;
using Scriba;

namespace PigeonPost;

internal sealed class ServerApp : BaseApp
{
    private readonly TunDevice _tun;
    private readonly ServerSideLogic _logic;
    
    public ServerApp(BridgeConfiguration config, ILogger logger) : base(config, logger)
    {
        if (config.Role != Role.Server)
            throw new ArgumentException("Role must be Server.", nameof(config));
        
        var tunName = _config.TunNames[0];

        _tun = new TunDevice(tunName);
        _tun.SetSendBufferSize(1048576);
        _logger.i($"TUN device '{tunName}' opened.");

        _logic = new ServerSideLogic(
            _tun,
            _config.PontifexUrl,
            _logger,
            _serverTransportFactory,
            _config.BufferSizeBytes,
            _config.Verbose);
    }

    public override void RequestShutdown()
    {
        _logic.Stop();
    }

    public override async Task RunAsync()
    {
        var stoppedTcs = new TaskCompletionSource();
        _logic.Stopped += () => stoppedTcs.TrySetResult();

        _logger.i("Server running. Accepting clients...");
        _logic.Start();

        await stoppedTcs.Task;
        
        _logic.Stop();
        _tun.Close();
        _logger.i("Server shut down.");
    }
}
