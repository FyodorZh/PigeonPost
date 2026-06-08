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
                case "--help" or "-h":
                    PrintHelp(errorWriter);
                    return null;
                default:
                    PrintError(errorWriter, $"Unknown argument: '{arg}'.");
                    return null;
            }
        }

        if (!Validate(role, tunNames, url, bufferSize, errorWriter))
            return null;

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

        int expectedTuns = role == Role.Debug ? 2 : 1;
        if (tunNames.Count != expectedTuns)
        {
            PrintError(errorWriter, $"--tun must be provided {expectedTuns} time(s) for role '{role}'.");
            return false;
        }

        if (bufferSize < 1500) { PrintError(errorWriter, "--buffer-size must be at least 1500 bytes."); return false; }
        if (bufferSize > 1_073_741_824) { PrintError(errorWriter, "--buffer-size must be at most 1_073_741_824 bytes (1 GB)."); return false; }

        return true;
    }

    private static void PrintError(TextWriter w, string msg) => w.WriteLine($"Error: {msg}");
    private static void PrintHelp(TextWriter w)
    {
        w.WriteLine("Usage: PigeonPost --role <server|client|debug> --tun <name> [--tun <name2>] --url <url> [options]");
        w.WriteLine();
        w.WriteLine("Arguments:");
        w.WriteLine("  -r, --role          Role: server, client, or debug.");
        w.WriteLine("  -t, --tun           TUN device name (repeatable; once for server/client, twice for debug).");
        w.WriteLine("  -u, --url           Pontifex transport URL (e.g. \"tcp|127.0.0.1:9000/30\", \"direct|ep_name\").");
        w.WriteLine("  -b, --buffer-size   Packet buffer size in bytes (default: 10485760 = 10 MB).");
        w.WriteLine("  -v, --verbose       Log all packet sizes (in/out).");
        w.WriteLine("  -h, --help          Show this help.");
    }
}
