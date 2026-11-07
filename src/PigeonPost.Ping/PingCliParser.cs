using System;
using System.Globalization;
using System.IO;

namespace PigeonPost.Ping;

internal enum ParseOutcome
{
    Success,
    ShowHelp,
    Error
}

internal static class PingCliParser
{
    public static ParseOutcome Parse(string[] args, TextWriter stdout, TextWriter stderr, out PingOptions? options)
    {
        options = null;

        bool listen = false;
        bool intervalSet = false;
        bool sizeSet = false;
        bool waitSet = false;
        bool countSet = false;
        bool verbose = false;
        string? url = null;
        int intervalMs = 1000;
        int payloadSize = 100;
        int waitMs = 5000;
        int count = 0;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-l" or "--listen":
                    if (listen)
                    {
                        return Fail(stderr, "The listen option may only be provided once.");
                    }
                    if (url != null)
                    {
                        return Fail(stderr, "Only one URI may be provided.");
                    }
                    if (++i >= args.Length)
                    {
                        return Fail(stderr, "Missing value for --listen.");
                    }
                    listen = true;
                    url = args[i];
                    break;

                case "-i" or "--interval":
                    if (++i >= args.Length)
                    {
                        return Fail(stderr, "Missing value for --interval.");
                    }
                    if (!TryParseNonNegative(args[i], out intervalMs))
                    {
                        return Fail(stderr, $"Invalid interval: '{args[i]}'.");
                    }
                    intervalSet = true;
                    break;

                case "-s" or "--size":
                    if (++i >= args.Length)
                    {
                        return Fail(stderr, "Missing value for --size.");
                    }
                    if (!TryParseNonNegative(args[i], out payloadSize))
                    {
                        return Fail(stderr, $"Invalid packet size: '{args[i]}'.");
                    }
                    sizeSet = true;
                    break;

                case "-w" or "--wait":
                    if (++i >= args.Length)
                    {
                        return Fail(stderr, "Missing value for --wait.");
                    }
                    if (!TryParseNonNegative(args[i], out waitMs))
                    {
                        return Fail(stderr, $"Invalid wait time: '{args[i]}'.");
                    }
                    waitSet = true;
                    break;

                case "-c" or "--count":
                    if (++i >= args.Length)
                    {
                        return Fail(stderr, "Missing value for --count.");
                    }
                    if (!TryParseNonNegative(args[i], out count))
                    {
                        return Fail(stderr, $"Invalid count: '{args[i]}'.");
                    }
                    countSet = true;
                    break;

                case "-v" or "--verbose":
                    verbose = true;
                    break;

                case "-h" or "--help" or "/help":
                    PrintHelp(stdout);
                    return ParseOutcome.ShowHelp;

                default:
                    if (arg.StartsWith('-'))
                    {
                        return Fail(stderr, $"Unknown argument: '{arg}'.");
                    }
                    if (url != null)
                    {
                        return Fail(stderr, "Only one URI may be provided.");
                    }
                    url = arg;
                    break;
            }
        }

        if (url == null)
        {
            return Fail(stderr, "A URI is required.");
        }

        if (listen && (intervalSet || sizeSet || waitSet || countSet))
        {
            return Fail(stderr, "Client options (-i, -s, -w, -c) are not valid in server mode.");
        }

        if (intervalMs < 1)
        {
            return Fail(stderr, "--interval must be at least 1 ms.");
        }

        if (payloadSize < 1)
        {
            return Fail(stderr, "--size must be at least 1 byte.");
        }

        if (payloadSize > PingProtocol.MaxPayloadSize)
        {
            return Fail(stderr, $"--size must be at most {PingProtocol.MaxPayloadSize} bytes.");
        }

        if (waitMs < 1)
        {
            return Fail(stderr, "--wait must be at least 1 ms.");
        }

        options = new PingOptions
        {
            Mode = listen ? PingMode.Server : PingMode.Client,
            Url = url,
            IntervalMs = intervalMs,
            PayloadSize = payloadSize,
            WaitMs = waitMs,
            Count = count,
            Verbose = verbose
        };

        return ParseOutcome.Success;
    }

    private static bool TryParseNonNegative(string value, out int result)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);
    }

    private static ParseOutcome Fail(TextWriter stderr, string message)
    {
        stderr.WriteLine($"Error: {message}");
        stderr.WriteLine();
        PrintHelp(stderr);
        return ParseOutcome.Error;
    }

    private static void PrintHelp(TextWriter w)
    {
        w.WriteLine("PIGEONPOST.PING(1)              User Commands             PIGEONPOST.PING(1)");
        w.WriteLine();
        w.WriteLine("NAME");
        w.WriteLine("    PigeonPost.Ping - verify a Pontifex connection by echoing random data");
        w.WriteLine();
        w.WriteLine("SYNOPSIS");
        w.WriteLine("    PigeonPost.Ping -l <url>");
        w.WriteLine("    PigeonPost.Ping <url> [-i <ms>] [-s <bytes>] [-w <ms>]");
        w.WriteLine("                     [-c <count>] [-v]");
        w.WriteLine();
        w.WriteLine("DESCRIPTION");
        w.WriteLine("    PigeonPost.Ping runs either a Pontifex server or client and checks");
        w.WriteLine("    that data survives a round trip unchanged. The client periodically");
        w.WriteLine("    sends a random payload. The server reverses the payload bytes and");
        w.WriteLine("    echoes the message back. The client verifies the reversed payload");
        w.WriteLine("    and reports the round-trip time.");
        w.WriteLine();
        w.WriteLine("    Multiple clients may connect to one server concurrently. Each");
        w.WriteLine("    connection is handled independently.");
        w.WriteLine();
        w.WriteLine("OPTIONS");
        w.WriteLine("    -l, --listen <url>");
        w.WriteLine("        Run as a server and listen on the given Pontifex transport URL.");
        w.WriteLine("        Mutually exclusive with the client URI.");
        w.WriteLine();
        w.WriteLine("    <url>");
        w.WriteLine("        Client mode: connect to this Pontifex transport URL.");
        w.WriteLine("        The value must be quoted to protect the '|' character.");
        w.WriteLine("        Example: 'tcp|127.0.0.1:9700/30'");
        w.WriteLine();
        w.WriteLine("    -i, --interval <ms>");
        w.WriteLine("        Milliseconds between sends. Default: 1000.");
        w.WriteLine();
        w.WriteLine("    -s, --size <bytes>");
        w.WriteLine("        Number of random payload bytes per message. Default: 100.");
        w.WriteLine("        Valid range: 1 to 1048576.");
        w.WriteLine();
        w.WriteLine("    -w, --wait <ms>");
        w.WriteLine("        Milliseconds to wait for each reply before reporting a");
        w.WriteLine("        timeout. Default: 5000.");
        w.WriteLine();
        w.WriteLine("    -c, --count <count>");
        w.WriteLine("        Number of messages to send before stopping and printing a");
        w.WriteLine("        summary. Default: 0 (run until interrupted).");
        w.WriteLine();
        w.WriteLine("    -v, --verbose");
        w.WriteLine("        Write Pontifex transport diagnostics to stderr.");
        w.WriteLine();
        w.WriteLine("    -h, --help");
        w.WriteLine("        Display this help and exit.");
        w.WriteLine();
        w.WriteLine("EXAMPLES");
        w.WriteLine("    PigeonPost.Ping -l 'tcp|0.0.0.0:9700/30'");
        w.WriteLine("    PigeonPost.Ping 'tcp|127.0.0.1:9700/30' -c 10");
        w.WriteLine("    PigeonPost.Ping 'tcp|127.0.0.1:9700/30' -i 500 -s 1024 -w 2000");
        w.WriteLine();
        w.WriteLine("EXIT STATUS");
        w.WriteLine("    0    All probes were replied to and verified, or the run was");
        w.WriteLine("         interrupted by the user.");
        w.WriteLine("    1    Usage error, failed to create the transport, or failed to");
        w.WriteLine("         connect.");
        w.WriteLine("    2    The run completed with lost or corrupted replies.");
        w.WriteLine();
    }
}
