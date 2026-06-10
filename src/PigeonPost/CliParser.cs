using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PigeonPost;

internal static class CliParser
{
    public static BridgeConfiguration? Parse(string[] args, TextWriter errorWriter)
    {
        Role? role = null;
        var tunNames = new List<string>();
        string? url = null;
        int bufferSize = 10 * 1024 * 1024;
        bool verbose = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--role" or "-r":
                    if (++i >= args.Length) { PrintError(errorWriter, "Missing value for --role."); return null; }
                    if (!Enum.TryParse(args[i], ignoreCase: true, out Role parsed)) { PrintError(errorWriter, $"Invalid role: '{args[i]}'."); return null; }
                    role = parsed;
                    break;
                case "--tun" or "-t":
                    if (++i >= args.Length) { PrintError(errorWriter, "Missing value for --tun."); return null; }
                    tunNames.Add(args[i]);
                    break;
                case "--url" or "-u":
                    if (++i >= args.Length) { PrintError(errorWriter, "Missing value for --url."); return null; }
                    url = args[i];
                    break;
                case "--buffer-size" or "-b":
                    if (++i >= args.Length) { PrintError(errorWriter, "Missing value for --buffer-size."); return null; }
                    if (!int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out bufferSize))
                        { PrintError(errorWriter, $"Invalid buffer size: '{args[i]}'."); return null; }
                    break;
                case "--verbose" or "-v":
                    verbose = true;
                    break;
                case "--help" or "-h" or "/help":
                    PrintHelp(errorWriter);
                    return null;
                default:
                    PrintError(errorWriter, $"Unknown argument: '{arg}'.");
                    return null;
            }
        }

        if (!Validate(role, tunNames, url, bufferSize, errorWriter))
            return null;

        if (role == Role.Debug)
        {
            if (tunNames.Count == 0) { tunNames.Add("tunA"); tunNames.Add("tunB"); }
            else if (tunNames.Count == 1) tunNames.Add("tunB");
        }

        return new BridgeConfiguration
        {
            Role = role!.Value,
            TunNames = tunNames,
            PontifexUrl = url!,
            BufferSizeBytes = bufferSize,
            Verbose = verbose
        };
    }

    private static bool Validate(Role? role, List<string> tunNames, string? url, int bufferSize, TextWriter errorWriter)
    {
        if (role == null) { PrintError(errorWriter, "--role is required."); return false; }
        if (url == null) { PrintError(errorWriter, "--url is required."); return false; }

        if (role != Role.Debug && tunNames.Count != 1)
        {
            PrintError(errorWriter, "--tun must be provided once for this role.");
            return false;
        }

        if (bufferSize < 1500) { PrintError(errorWriter, "--buffer-size must be at least 1500 bytes."); return false; }
        if (bufferSize > 1_073_741_824) { PrintError(errorWriter, "--buffer-size must be at most 1_073_741_824 bytes (1 GB)."); return false; }

        return true;
    }

    private static void PrintError(TextWriter w, string msg)
    {
        w.WriteLine($"Error: {msg}");
        w.WriteLine();
        PrintHelp(w);
    }
    private static void PrintHelp(TextWriter w)
    {
        w.WriteLine("PIGEONPOST(1)                    User Commands                    PIGEONPOST(1)");
        w.WriteLine();
        w.WriteLine("NAME");
        w.WriteLine("    PigeonPost - bridge TUN virtual network devices over a Pontifex");
        w.WriteLine("    transport");
        w.WriteLine();
        w.WriteLine("SYNOPSIS");
        w.WriteLine("    PigeonPost --role <server|client|debug> --tun <name>");
        w.WriteLine("              [--tun <name2>] --url <url> [options]");
        w.WriteLine();
        w.WriteLine("DESCRIPTION");
        w.WriteLine("    PigeonPost bridges two TUN virtual network devices over a Pontifex");
        w.WriteLine("    transport layer, creating a P2P bidirectional IP tunnel between two");
        w.WriteLine("    Linux machines.");
        w.WriteLine();
        w.WriteLine("    Roles:");
        w.WriteLine("    server    Listens for a single Pontifex client connection and bridges");
        w.WriteLine("              to one TUN device.");
        w.WriteLine("    client    Connects to a Pontifex server and bridges to one TUN device.");
        w.WriteLine("              Automatically reconnects on disconnect.");
        w.WriteLine("    debug     Single-process mode running both server and client with two");
        w.WriteLine("              TUN devices using in-process Direct Pontifex transport.");
        w.WriteLine();
        w.WriteLine("    PigeonPost opens existing TUN devices. It does not create or configure");
        w.WriteLine("    them. IP addresses and routes must be set up externally.");
        w.WriteLine();
        w.WriteLine("OPTIONS");
        w.WriteLine("    -r, --role <role>");
        w.WriteLine("        Required. Runtime role: server, client, or debug.");
        w.WriteLine();
        w.WriteLine("    -t, --tun <name>");
        w.WriteLine("        TUN device name (e.g. tun0). Repeatable. Provide once for");
        w.WriteLine("        server/client roles. Optional in debug mode (defaults to");
        w.WriteLine("        tunA and tunB).");
        w.WriteLine();
        w.WriteLine("    -u, --url <url>");
        w.WriteLine("        Required. Pontifex transport URL. Must be quoted to protect the");
        w.WriteLine("        '|' character from the shell. Examples:");
        w.WriteLine("        'tcp|127.0.0.1:9000/30'      TCP transport (quoted)");
        w.WriteLine("        'direct|ep_name'             Direct transport, debug (quoted)");
        w.WriteLine();
        w.WriteLine("    -b, --buffer-size <bytes>");
        w.WriteLine("        Outgoing packet buffer size in bytes. Must be between 1500 and");
        w.WriteLine("        1,073,741,824 (1 GB). Newest packets dropped when full.");
        w.WriteLine("        Default: 10485760 (10 MB).");
        w.WriteLine();
        w.WriteLine("    -v, --verbose");
        w.WriteLine("        Log all packet sizes for sent and received traffic.");
        w.WriteLine();
        w.WriteLine("    -h, --help");
        w.WriteLine("        Display this help and exit.");
        w.WriteLine();
        w.WriteLine("BEHAVIOR");
        w.WriteLine("    The TUN reader starts immediately at launch, before the Pontifex");
        w.WriteLine("    connection is established. Outgoing packets (TUN -> Pontifex) are");
        w.WriteLine("    buffered while the transport is not connected. Incoming packets");
        w.WriteLine("    (Pontifex -> TUN) are written directly with no buffering.");
        w.WriteLine();
        w.WriteLine("    The application handles SIGTERM and SIGINT gracefully: drains");
        w.WriteLine("    buffered packets, closes the transport, and closes all TUN file");
        w.WriteLine("    descriptors.");
        w.WriteLine();
        w.WriteLine("EXAMPLES");
        w.WriteLine("    Server on TCP port 9000, bridging tun0:");
        w.WriteLine("        PigeonPost --role server --tun tun0 --url 'tcp|0.0.0.0:9000/30'");
        w.WriteLine();
        w.WriteLine("    Client connecting to the server, bridging tun1:");
        w.WriteLine("        PigeonPost --role client --tun tun1 --url 'tcp|10.0.0.1:9000/30'");
        w.WriteLine();
        w.WriteLine("    Debug mode with two TUN devices:");
        w.WriteLine("        PigeonPost --role debug --tun tun0 --tun tun1 --url 'direct|ep_debug'");
        w.WriteLine();
        w.WriteLine("PROJECT");
        w.WriteLine("    PigeonPost.Tun      TUN device abstraction: open, close, read, write");
        w.WriteLine("    PigeonPost.Bridge   Core bridging: packet buffering, transport handlers");
        w.WriteLine("    PigeonPost          Entry point, CLI parsing, signal handling");
        w.WriteLine();
        w.WriteLine("PigeonPost 1.0                        June 2026                     PIGEONPOST(1)");
    }
}
