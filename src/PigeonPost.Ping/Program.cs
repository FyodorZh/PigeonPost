using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Scriba;

namespace PigeonPost.Ping;

internal static class Program
{
    private static PingServer? _server;
    private static PingClient? _client;

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

        if (options.Verbose)
        {
            StaticLogger.Instance.AddConsumer(new StderrLogConsumer());
            StaticLogger.Instance.LogTime = true;
        }

        ILogger logger = StaticLogger.Instance;

        if (options.Mode == PingMode.Server)
        {
            _server = new PingServer(options.Url, logger);
        }
        else
        {
            _client = new PingClient(options, logger);
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
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (options.Verbose)
            {
                logger.wtf(ex);
            }
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
        if (_client != null)
        {
            Console.Error.WriteLine($"Received {signal}. Shutting down...");
            _client.RequestShutdown();
            return;
        }

        if (_server != null)
        {
            Console.Out.WriteLine($"Received {signal}. Shutting down...");
            _server.Stop();
        }
    }
}

internal sealed class StderrLogConsumer : MultiRefLogConsumer
{
    public ILogFormatter Formatter { get; set; } = new SynchronizedLogFormatter(DefaultFormatter);

    public override void Message(MessageData logMessage)
    {
        Formatter.Format(logMessage, Console.Error);
    }

    private static void DefaultFormatter(MessageData logMessage, TextWriter dst)
    {
        dst.Write(logMessage.Severity);
        dst.Write(": ");
        if (logMessage.WriteTimeTo(dst))
        {
            dst.Write(": ");
        }
        logMessage.WriteMessageTo(dst);
        logMessage.WriteStackTrace("\t", dst);
        dst.WriteLine();
    }
}
