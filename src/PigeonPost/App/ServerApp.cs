using System;
using System.Threading.Tasks;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Servers;
using Pontifex.StopReasons;
using PigeonPost.Bridge;
using PigeonPost.Tun;
using Scriba;

namespace PigeonPost;

internal sealed class ServerApp : BaseApp
{
    public ServerApp(BridgeConfiguration config, ILogger logger) : base(config, logger)
    {
        if (config.Role != Role.Server)
            throw new ArgumentException("Role must be Server.", nameof(config));
    }

    public override async Task RunAsync()
    {
        var tunName = _config.TunNames[0];

        using var tun = new TunDevice(tunName);
        tun.SetSendBufferSize(1048576);
        _logger.i($"TUN device '{tunName}' opened.");

        var serverHub = new ServerHub(_logger, tun);
        var buffer = new PacketBuffer(_config.BufferSizeBytes);

        using var bridge = new BridgeImpl(tun, buffer, _logger, _config.Verbose);
        bridge.SetPacketHandler(packet => serverHub.OnPacketFromTun(packet));

        var transport = CreateTransport(_config.PontifexUrl, isServer: true);
        if (transport is not IAckRawServer ackServer)
            throw new InvalidOperationException("Transport is not an IAckRawServer.");

        ackServer.Init(new BridgeServerAcknowledger(serverHub));

        bridge.Start();

        var stopped = new TaskCompletionSource<StopReason>();
        bridge.OnStopped += reason => stopped.TrySetResult(reason);

        ackServer.Start(reason =>
        {
            _logger.i($"Server stopped: {reason.Type}");
            stopped.TrySetResult(reason);
        });

        _logger.i("Server running. Accepting clients...");

        var result = await Task.WhenAny(
            stopped.Task,
            WaitForShutdownAsync()
        );

        if (result == stopped.Task)
        {
            _logger.w("Transport stopped unexpectedly. Exiting.");
        }

        serverHub.StopAccepting();
        serverHub.StopAll(Pontifex.StopReason.UserIntention);
        bridge.Stop(Pontifex.StopReason.UserIntention);
        ackServer.Stop(Pontifex.StopReason.UserIntention);
        tun.Close();
        _logger.i("Server shut down.");
    }
}
