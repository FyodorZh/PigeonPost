using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Scriba;

namespace PigeonPost.Ping;

internal static class Program
{
    private static PingServer? _server;
    private static PingClient? _client;
    private static PingLog? _log;

    private static async Task<int> Main(string[] args)
    {
        ParseOutcome outcome = PingCliParser.Parse(args, Console.Out, Console.Error, out PingOptions? options);
        if (outcome == ParseOutcome.ShowHelp)
        {
            return 0;
        }

        if (outcome == ParseOutcome.Error || options == null)
        {
            return 1;
        }

        string role = options.Mode == PingMode.Server ? "server" : "client";
        _log = new PingLog(role, options.Verbose);
        StaticLogger.Instance.AddConsumer(new TransportLogConsumer(role, options.Verbose));
        StaticLogger.Instance.LogTime = true;
        ILogger logger = StaticLogger.Instance;

        if (options.Mode == PingMode.Server)
        {
            _server = new PingServer(options, logger, _log);
        }
        else
        {
            _client = new PingClient(options, logger, _log);
        }

        PosixSignalRegistration? sigterm = null;
        PosixSignalRegistration? sigint = null;

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                RequestShutdown("SIGTERM");
            });

            sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
            {
                context.Cancel = true;
                RequestShutdown("SIGINT");
            });
        }

        try
        {
            if (_server != null)
            {
                await _server.RunAsync();
                return 0;
            }

            return _client != null ? await _client.RunAsync() : 1;
        }
        catch (Exception ex)
        {
            _log.Error(
                "fatal",
                ("message", ex.Message),
                ("exception", ex.ToString()));
            return 1;
        }
        finally
        {
            sigterm?.Dispose();
            sigint?.Dispose();
        }
    }

    private static void RequestShutdown(string signal)
    {
        _log?.Event("signal", ("signal", signal));

        if (_client != null)
        {
            _client.RequestShutdown();
            return;
        }

        _server?.Stop();
    }
}
