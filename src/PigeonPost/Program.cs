using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Scriba;

namespace PigeonPost;

internal static class Program
{
    private static BaseApp? _app;

    public class ConsoleConsumer2 : MultiRefLogConsumer
    {
        public ILogFormatter Formatter { get; set; } = new SynchronizedLogFormatter(DefaultFormatter);
        
        public override void Message(MessageData logMessage)
        {
            Formatter.Format(logMessage, Console.Out);
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

    static async Task<int> Main(string[] args)
    {
        var config = CliParser.Parse(args, Console.Error);
        if (config == null)
            return 1;

        StaticLogger.Instance.AddConsumer(new ConsoleConsumer2());
        StaticLogger.Instance.LogTime = true;
        var logger = StaticLogger.Instance;

        _app = config.Role switch
        {
            Role.Server => new ServerApp(config, logger),
            Role.Client => new ClientApp(config, logger),
            Role.Debug => new DebugApp(config, logger),
            _ => throw new InvalidOperationException($"Unknown role: {config.Role}")
        };

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
            await _app.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            logger.wtf(ex);
            return 1;
        }
    }
}
