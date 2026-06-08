# Stage 8: Console Host & App Orchestration

## Goal

Implement the console application entry point (`Program.cs`), app orchestration,
Pontifex transport creation, reconnection loop, and graceful SIGTERM/SIGINT handling.

## Prerequisites

- Stage 4 complete (CLI parsing, `BridgeConfiguration`).
- Stage 7 complete (Bridge class with `OnStopped` event).

## Architecture

```
Program.Main()
  ├── Parse CLI args → BridgeConfiguration
  ├── Setup logging (Scriba ConsoleConsumer)
  ├── Register SIGTERM/SIGINT handlers
  ├── Create App instance per configured role
  │     └── App.Run(config):
  │           ├── Server mode:  App.RunServer()
  │           ├── Client mode:  App.RunClient()
  │           └── Debug mode:   App.RunDebug()
  └── Wait for shutdown signal
```

## Implementation

### `App` class

File: `src/PigeonPost/App.cs`

```csharp
using System.Net;
using System.Threading;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Endpoints;
using Pontifex.Transports.Direct;
using Pontifex.Transports.Tcp;
using PigeonPost.Bridge;
using PigeonPost.Bridge.Handlers;
using PigeonPost.Tun;
using Scriba;

namespace PigeonPost;

internal sealed class App
{
    private readonly BridgeConfiguration _config;
    private readonly ILogger _logger;

    private volatile bool _shutdownRequested;
    private readonly CancellationTokenSource _cts = new();

    public App(BridgeConfiguration config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        switch (_config.Role)
        {
            case Role.Server:
                await RunServerAsync();
                break;
            case Role.Client:
                await RunClientAsync();
                break;
            case Role.Debug:
                await RunDebugAsync();
                break;
        }
    }

    public void RequestShutdown()
    {
        _shutdownRequested = true;
        _cts.Cancel();
    }

    // --- Server ---

    private async Task RunServerAsync()
    {
        var tunName = _config.TunNames[0];

        // 1. Open TUN device
        using var tun = new TunDevice();
        tun.Open(tunName);
        _logger.i($"TUN device '{tunName}' opened.");

        // 2. Create buffer
        var buffer = new PacketBuffer(_config.BufferSizeBytes);

        // 3. Create bridge
        using var bridge = new Bridge(tun, buffer, _logger, _config.Verbose);

        // 4. Create Pontifex server transport
        var transport = CreateTransport(_config.PontifexUrl, isServer: true);
        if (transport is not IAckRawServer ackServer)
            throw new InvalidOperationException("Transport is not an IAckRawServer.");

        ackServer.Init(new BridgeServerAcknowledger(bridge));

        // 5. Start
        bridge.Start();

        var stopped = new TaskCompletionSource<StopReason>();
        bridge.OnStopped += reason => stopped.TrySetResult(reason);

        ackServer.Start(reason =>
        {
            _logger.i($"Server stopped: {reason.Type}");
            stopped.TrySetResult(reason);
        });

        _logger.i("Server running. Waiting for client...");

        // 6. Wait for shutdown signal or stop reason
        var result = await Task.WhenAny(
            stopped.Task,
            WaitForShutdownAsync()
        );

        if (result == stopped.Task)
        {
            _logger.w("Transport stopped unexpectedly. Exiting.");
        }

        // 7. Cleanup
        bridge.Stop(StopReason.UserIntention);
        ackServer.Stop(StopReason.UserIntention);
        tun.Close();
        _logger.i("Server shut down.");
    }

    // --- Client ---

    private async Task RunClientAsync()
    {
        var tunName = _config.TunNames[0];

        // 1. Open TUN device
        using var tun = new TunDevice();
        tun.Open(tunName);
        _logger.i($"TUN device '{tunName}' opened.");

        // 2. Create buffer
        var buffer = new PacketBuffer(_config.BufferSizeBytes);

        // 3. Create bridge (single instance, reused across reconnections)
        using var bridge = new Bridge(tun, buffer, _logger, _config.Verbose);
        bridge.Start();

        // 4. Reconnection loop
        while (!_shutdownRequested)
        {
            _logger.i("Connecting to server...");

            var transport = CreateTransport(_config.PontifexUrl, isServer: false);
            if (transport is not IAckRawClient ackClient)
                throw new InvalidOperationException("Transport is not an IAckRawClient.");

            var handler = new BridgeClientHandler(bridge);
            ackClient.Init(handler);

            var stopped = new TaskCompletionSource<StopReason>();
            bridge.OnStopped += reason => stopped.TrySetResult(reason);

            ackClient.Start(reason =>
            {
                _logger.i($"Client stopped: {reason.Type}");
                stopped.TrySetResult(reason);
            });

            _logger.i("Client connected.");

            // Wait until stopped, or shutdown requested
            var result = await Task.WhenAny(
                stopped.Task,
                WaitForShutdownAsync()
            );

            if (_shutdownRequested)
            {
                ackClient.Stop(StopReason.UserIntention);
                break;
            }

            _logger.i("Connection lost. Reconnecting in 1 second...");
            await Task.Delay(1000, _cts.Token);
        }

        bridge.Stop(StopReason.UserIntention);
        tun.Close();
        _logger.i("Client shut down.");
    }

    // --- Debug ---

    private async Task RunDebugAsync()
    {
        var tunName1 = _config.TunNames[0];
        var tunName2 = _config.TunNames[1];

        // 1. Open both TUN devices
        using var tun1 = new TunDevice();
        using var tun2 = new TunDevice();
        tun1.Open(tunName1);
        tun2.Open(tunName2);
        _logger.i($"TUN devices '{tunName1}' and '{tunName2}' opened.");

        // 2. Create two bridges
        var buffer1 = new PacketBuffer(_config.BufferSizeBytes);
        var buffer2 = new PacketBuffer(_config.BufferSizeBytes);
        using var bridge1 = new Bridge(tun1, buffer1, _logger, _config.Verbose);
        using var bridge2 = new Bridge(tun2, buffer2, _logger, _config.Verbose);

        // 3. Create Direct transport (server + client)
        string serverName = _config.PontifexUrl;
        // For debug mode, URL should be "direct|server_name"
        // Extract server name from URL
        var serverNameActual = ExtractDirectServerName(_config.PontifexUrl);

        var server = new AckRawDirectServer(serverNameActual, _logger, MemoryRental.Shared);
        server.Init(new BridgeServerAcknowledger(bridge1));

        var client = new AckRawDirectClient(serverNameActual, _logger, MemoryRental.Shared);
        client.Init(new BridgeClientHandler(bridge2));

        // 4. Start bridges
        bridge1.Start();
        bridge2.Start();

        // 5. Start server and client
        server.Start(reason => _logger.i($"Debug server stopped: {reason.Type}"));
        client.Start(reason => _logger.i($"Debug client stopped: {reason.Type}"));

        _logger.i($"Debug mode running: {tunName1} ←→ {tunName2}");

        // 6. Wait for shutdown
        await WaitForShutdownAsync();

        // 7. Cleanup
        client.Stop(StopReason.UserIntention);
        bridge2.Stop(StopReason.UserIntention);
        bridge1.Stop(StopReason.UserIntention);
        tun2.Close();
        tun1.Close();
        _logger.i("Debug instance shut down.");
    }

    // --- Helpers ---

    private ITransport CreateTransport(string url, bool isServer)
    {
        var factory = new TransportFactory();
        var reg = new TransportFactoryRegistrator(factory);
        reg.Register<AckRawTcpServerProducer>();
        reg.Register<AckRawTcpClientProducer>();
        reg.Register<AckRawReconnectableServerProducer>();
        reg.Register<AckRawReconnectableClientProducer>();

        var transport = factory.Construct(url, _logger, MemoryRental.Shared);
        if (transport == null)
            throw new InvalidOperationException($"Failed to construct transport from URL: '{url}'");
        return transport;
    }

    private static string ExtractDirectServerName(string url)
    {
        // "direct|server_name" → "server_name"
        const string prefix = "direct|";
        if (url.StartsWith(prefix))
            return url.Substring(prefix.Length);
        throw new ArgumentException($"Expected direct transport URL, got: '{url}'");
    }

    private async Task WaitForShutdownAsync()
    {
        try
        {
            await Task.Delay(Timeout.Infinite, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested — normal.
        }
    }
}
```

### `Program.cs`

File: `src/PigeonPost/Program.cs`

```csharp
using System.Runtime.InteropServices;
using Scriba;
using Scriba.Consumers;

namespace PigeonPost;

internal static class Program
{
    private static App? _app;

    static int Main(string[] args)
    {
        // Parse CLI
        var config = CliParser.Parse(args, Console.Error);
        if (config == null)
            return 1;

        // Setup logging
        StaticLogger.Instance.AddConsumer(new ConsoleConsumer());
        var logger = StaticLogger.Instance;

        // Create app
        _app = new App(config, logger);

        // Register signal handlers (Linux)
        if (OperatingSystem.IsLinux())
        {
            PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx =>
            {
                logger.i("Received SIGTERM. Shutting down...");
                ctx.Cancel = true;
                _app.RequestShutdown();
            });

            PosixSignalRegistration.Create(PosixSignal.SIGINT, ctx =>
            {
                logger.i("Received SIGINT. Shutting down...");
                ctx.Cancel = true;
                _app.RequestShutdown();
            });
        }

        try
        {
            _app.RunAsync().GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception ex)
        {
            logger.wtf(ex);
            return 1;
        }
    }
}
```

### Reconnectable Protocol Import

For `AckRawReconnectableServerProducer` / `AckRawReconnectableClientProducer`,
we need to add `Pontifex.Protocol.Reconnectable` package reference to the host project.

However, as noted in the Pontifex docs, for V1 we're doing manual reconnection.
The `CreateTransport` method uses the factory to construct TCP or Direct transports.
If the URL starts with `reconnectable|`, the factory should handle it — but we'll
only use this in V2. For V1, the URL format is:
- `direct|server_name` for debug mode
- `tcp|ip:port/timeout` for real mode

Simplified `CreateTransport` for V1:

```csharp
private ITransport CreateTransport(string url, bool isServer)
{
    var factory = new TransportFactory();
    var reg = new TransportFactoryRegistrator(factory);
    reg.Register<AckRawTcpServerProducer>();
    reg.Register<AckRawTcpClientProducer>();

    // For "direct|name", construct manually since Direct producers may not
    // be registered via the factory (they don't auto-register in all versions).
    if (url.StartsWith("direct|"))
    {
        string name = url.Substring("direct|".Length);
        if (isServer)
            return new AckRawDirectServer(name, _logger, MemoryRental.Shared);
        else
            return new AckRawDirectClient(name, _logger, MemoryRental.Shared);
    }

    var transport = factory.Construct(url, _logger, MemoryRental.Shared);
    if (transport == null)
        throw new InvalidOperationException($"Failed to construct transport from URL: '{url}'");

    return transport;
}
```

**Note:** The `TransportFactory` and `TransportFactoryRegistrator` types need to be
verified as `public`. If they're `internal`, we'll construct TCP types directly
(using reflection or by adding `InternalsVisibleTo` if the Pontifex packages support it).
The fallback is to use Direct transport directly (always public) and handle TCP
construction manually. We'll resolve this during implementation.

## Tests (PigeonPost.Tests)

### End-to-End Test: Debug mode with Direct transport

```csharp
[TestFixture]
[Category("Integration")]
public class AppIntegrationTests
{
    [Test]
    public void DebugMode_DirectTransport_TwoFakeTuns_PacketsFlow()
    {
        // This test is hard to run without real TUN devices.
        // For now, it validates that the app doesn't crash.
        // Full E2E with real TUNs comes in Stage 9.
    }
}
```

## Success Criteria

1. `PigeonPost --role server --tun tunX --url tcp|0.0.0.0:9000/30` starts a server.
2. `PigeonPost --role client --tun tunY --url tcp|127.0.0.1:9000/30` starts a client and connects.
3. `PigeonPost --role debug --tun tunA --tun tunB --url direct|ep` starts both server and client.
4. `Ctrl+C` (SIGINT) triggers graceful shutdown.
5. `kill <pid>` (SIGTERM) triggers graceful shutdown.
6. Client reconnects infinitely when the server restarts.
7. `--verbose` logs packet sizes.
8. `--help` prints usage.

## Files to Create/Modify

| File | Action |
|------|--------|
| `src/PigeonPost/App.cs` | Create |
| `src/PigeonPost/Program.cs` | Replace |
| `src/PigeonPost/PigeonPost.csproj` | Update with new references |
| `tests/PigeonPost.Tests/AppIntegrationTests.cs` | Create |
