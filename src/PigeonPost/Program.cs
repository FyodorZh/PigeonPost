using System;
using System.Runtime.InteropServices;
using Scriba;
using Scriba.Consumers;

namespace PigeonPost;

internal static class Program
{
    private static App? _app;

    static int Main(string[] args)
    {
        var config = CliParser.Parse(args, Console.Error);
        if (config == null)
            return 1;

        StaticLogger.Instance.AddConsumer(new ConsoleConsumer());
        var logger = StaticLogger.Instance;

        _app = new App(config, logger);

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
