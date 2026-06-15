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
        string? clientId = null;
        int debugClientCount = 1;

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
                case "--client-id":
                    if (++i >= args.Length) { PrintError(errorWriter, "Missing value for --client-id."); return null; }
                    clientId = args[i];
                    break;
                case "--debug-clients":
                    if (++i >= args.Length) { PrintError(errorWriter, "Missing value for --debug-clients."); return null; }
                    if (!int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out debugClientCount))
                        { PrintError(errorWriter, $"Invalid debug-clients: '{args[i]}'."); return null; }
                    break;
                case "--help" or "-h" or "/help":
                    PrintHelp(errorWriter);
                    return null;
                default:
                    PrintError(errorWriter, $"Unknown argument: '{arg}'.");
                    return null;
            }
        }

        if (!Validate(role, tunNames, url, bufferSize, clientId, debugClientCount, errorWriter))
            return null;

        if (role == Role.Debug)
        {
            int needed = debugClientCount;
            if (tunNames.Count == 0)
            {
                tunNames.Add("tunServer");
                for (int n = 1; n <= needed; n++)
                    tunNames.Add($"tunClient{n}");
            }
            else if (tunNames.Count == 1 && needed == 1)
            {
                tunNames.Add("tunB");
            }
            else if (tunNames.Count == needed)
            {
                tunNames.Insert(0, "tunServer");
            }
        }

        return new BridgeConfiguration
        {
            Role = role!.Value,
            TunNames = tunNames,
            PontifexUrl = url!,
            BufferSizeBytes = bufferSize,
            Verbose = verbose,
            ClientId = clientId,
            DebugClientCount = debugClientCount
        };
    }

    private static bool Validate(Role? role, List<string> tunNames, string? url,
        int bufferSize, string? clientId, int debugClientCount, TextWriter errorWriter)
    {
        if (role == null) { PrintError(errorWriter, "--role is required."); return false; }
        if (url == null) { PrintError(errorWriter, "--url is required."); return false; }

        if (role == Role.Client && string.IsNullOrEmpty(clientId))
        {
            PrintError(errorWriter, "--client-id is required for the client role.");
            return false;
        }

        if (role != Role.Client && !string.IsNullOrEmpty(clientId))
        {
            PrintError(errorWriter, "--client-id is only valid for the client role.");
            return false;
        }

        if (role == Role.Debug)
        {
            if (debugClientCount < 1)
            {
                PrintError(errorWriter, "--debug-clients must be at least 1.");
                return false;
            }
        }

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
        w.WriteLine("              [--tun <name2>] --url <url> [--client-id <id>]");
        w.WriteLine("              [--debug-clients <N>] [options]");
        w.WriteLine();
        w.WriteLine("DESCRIPTION");
        w.WriteLine("    PigeonPost bridges TUN virtual network devices over a Pontifex");
        w.WriteLine("    transport layer, creating a P2P bidirectional IP tunnel between two");
        w.WriteLine("    Linux machines. The server supports multiple concurrent clients,");
        w.WriteLine("    each identified by a unique clientId and advertising one IPv4 host");
        w.WriteLine("    route.");
        w.WriteLine();
        w.WriteLine("    Roles:");
        w.WriteLine("    server    Listens for Pontifex client connections and bridges them");
        w.WriteLine("              to one TUN device. Supports multiple concurrent clients.");
        w.WriteLine("    client    Connects to a Pontifex server, bridges to one TUN device.");
        w.WriteLine("              Requires --client-id. Automatically reconnects on");
        w.WriteLine("              disconnect.");
        w.WriteLine("    debug     Single-process mode running one server and N clients with");
        w.WriteLine("              N+1 TUN devices using in-process Pontifex transport.");
        w.WriteLine("              Control with --debug-clients (default 1).");
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
        w.WriteLine("        server/client roles. In debug mode, auto-generated or explicitly");
        w.WriteLine("        specified (server first, then clients).");
        w.WriteLine();
        w.WriteLine("    -u, --url <url>");
        w.WriteLine("        Required. Pontifex transport URL. Must be quoted to protect the");
        w.WriteLine("        '|' character from the shell. Examples:");
        w.WriteLine("        'tcp|127.0.0.1:9000/30'      TCP transport (quoted)");
        w.WriteLine("        'direct|ep_name'             Direct transport, debug (quoted)");
        w.WriteLine();
        w.WriteLine("    --client-id <id>");
        w.WriteLine("        Required for client role. Unique identifier sent during");
        w.WriteLine("        handshake. Duplicate clientId connections are rejected by the");
        w.WriteLine("        server.");
        w.WriteLine();
        w.WriteLine("    --debug-clients <N>");
        w.WriteLine("        Number of concurrent clients in debug mode. Default: 1.");
        w.WriteLine();
        w.WriteLine("    -b, --buffer-size <bytes>");
        w.WriteLine("        Outgoing packet buffer size in bytes. Must be between 1500 and");
        w.WriteLine("        1_073_741_824 (1 GB). Newest packets dropped when full.");
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
        w.WriteLine("        PigeonPost --role client --client-id office-a \\");
        w.WriteLine("            --tun tun1 --url 'tcp|10.0.0.1:9000/30'");
        w.WriteLine();
        w.WriteLine("    Debug mode with 3 clients:");
        w.WriteLine("        PigeonPost --role debug --debug-clients 3 --url 'direct|ep_debug'");
        w.WriteLine();
        w.WriteLine("PROJECT");
        w.WriteLine("    PigeonPost.Tun      TUN device abstraction: open, close, read, write");
        w.WriteLine("    PigeonPost.Bridge   Core bridging: packet buffering, transport handlers");
        w.WriteLine("    PigeonPost          Entry point, CLI parsing, signal handling");
        w.WriteLine();
        w.WriteLine("PigeonPost 1.0                        June 2026                     PIGEONPOST(1)");
    }
}
