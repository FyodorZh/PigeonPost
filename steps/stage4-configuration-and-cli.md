# Stage 4: Configuration & CLI Parsing

## Goal

Define the configuration model and implement command-line argument parsing.
This stage lives in `PigeonPost` (the console host project) since CLI is an app concern.

## Prerequisites

- Stage 1 complete (project structure builds).

## Configuration Model

### Role enum

```csharp
namespace PigeonPost;

public enum Role
{
    Server,
    Client,
    Debug
}
```

### BridgeConfiguration class

```csharp
namespace PigeonPost;

public sealed class BridgeConfiguration
{
    public Role Role { get; init; }
    public IReadOnlyList<string> TunNames { get; init; } = Array.Empty<string>();
    public string PontifexUrl { get; init; } = string.Empty;
    public int BufferSizeBytes { get; init; } = 10 * 1024 * 1024; // 10 MB
    public bool Verbose { get; init; }
}
```

### Validation Rules

| Rule | Error Message |
|------|---------------|
| `Role` must be specified. | `"--role is required."` |
| `TunNames.Count` == 1 for Server/Client, == 2 for Debug. | `"--tun must be provided once for Server/Client, twice for Debug."` |
| `PontifexUrl` must not be empty. | `"--url is required."` |
| `BufferSizeBytes` >= 1500 (one MTU). | `"--buffer-size must be at least 1500 bytes."` |
| `BufferSizeBytes` <= 1 GB (reasonable upper bound). | `"--buffer-size must be at most 1_073_741_824 bytes."` |

## CLI Arguments

```
PigeonPost --role <server|client|debug> --tun <name> [--tun <name2>] --url <pontifex-url> [--buffer-size <bytes>] [--verbose]
```

| Argument | Short | Type | Required | Default |
|----------|-------|------|----------|---------|
| `--role` | `-r` | string (`server`/`client`/`debug`) | Yes | — |
| `--tun` | `-t` | string (repeatable) | Yes | — |
| `--url` | `-u` | string | Yes | — |
| `--buffer-size` | `-b` | int | No | 10485760 |
| `--verbose` | `-v` | flag | No | false |

## Implementation Steps

### 4.1 Manual parser (no external library)

We use manual `string[]` parsing to avoid adding a dependency. The parser is simple
since there are only 5 arguments.

File: `src/PigeonPost/CliParser.cs`

```csharp
using System.Globalization;

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
    private static void PrintHelp(TextWriter w) { /* print usage */ }
}
```

### 4.2 Help Text

```
Usage: PigeonPost --role <server|client|debug> --tun <name> [--tun <name2>] --url <url> [options]

Arguments:
  -r, --role          Role: server, client, or debug.
  -t, --tun           TUN device name (repeatable; once for server/client, twice for debug).
  -u, --url           Pontifex transport URL (e.g. "tcp|127.0.0.1:9000/30", "direct|ep_name").
  -b, --buffer-size   Packet buffer size in bytes (default: 10485760 = 10 MB).
  -v, --verbose       Log all packet sizes (in/out).
  -h, --help          Show this help.
```

## Tests (PigeonPost.Tests)

### `CliParserTests`

```csharp
[TestFixture]
public class CliParserTests
{
    private static BridgeConfiguration Parse(params string[] args) => CliParser.Parse(args, TextWriter.Null)!;

    [Test]
    public void MinimalValidArgs_Server_ParsesCorrectly()
    {
        var cfg = Parse("--role", "server", "--tun", "tun0", "--url", "tcp|127.0.0.1:9000/30");
        Assert.That(cfg.Role, Is.EqualTo(Role.Server));
        Assert.That(cfg.TunNames, Is.EquivalentTo(new[] { "tun0" }));
        Assert.That(cfg.PontifexUrl, Is.EqualTo("tcp|127.0.0.1:9000/30"));
        Assert.That(cfg.BufferSizeBytes, Is.EqualTo(10_485_760));
        Assert.That(cfg.Verbose, Is.False);
    }

    [Test]
    public void MinimalValidArgs_Client_ParsesCorrectly()
    {
        var cfg = Parse("--role", "client", "-t", "tun1", "-u", "direct|ep1");
        Assert.That(cfg.Role, Is.EqualTo(Role.Client));
        Assert.That(cfg.TunNames[0], Is.EqualTo("tun1"));
    }

    [Test]
    public void Debug_Role_RequiresTwoTunNames()
    {
        var cfg = Parse("-r", "debug", "-t", "tunA", "-t", "tunB", "-u", "direct|ep");
        Assert.That(cfg.Role, Is.EqualTo(Role.Debug));
        Assert.That(cfg.TunNames, Is.EquivalentTo(new[] { "tunA", "tunB" }));
    }

    [Test]
    public void Debug_Role_WithOneTun_Fails()
    {
        var cfg = CliParser.Parse(new[] { "-r", "debug", "-t", "tunA", "-u", "direct|ep" }, TextWriter.Null);
        Assert.That(cfg, Is.Null);
    }

    [Test]
    public void MissingRole_Fails() => Assert.That(ParseOrNull("-t", "tun0", "-u", "url"), Is.Null);
    [TestFixture]
    public void MissingTun_Fails() => Assert.That(ParseOrNull("-r", "server", "-u", "url"), Is.Null);
    [Test]
    public void MissingUrl_Fails() => Assert.That(ParseOrNull("-r", "server", "-t", "tun0"), Is.Null);

    [Test]
    public void BufferSizeCustom_Parses()
    {
        var cfg = Parse("-r", "client", "-t", "t0", "-u", "url", "-b", "50000");
        Assert.That(cfg.BufferSizeBytes, Is.EqualTo(50_000));
    }

    [Test]
    public void BufferSizeBelowMinimum_Fails()
        => Assert.That(ParseOrNull("-r", "client", "-t", "t0", "-u", "url", "-b", "100"), Is.Null);

    [Test]
    public void VerboseFlag_SetsTrue()
    {
        var cfg = Parse("-r", "client", "-t", "t0", "-u", "url", "-v");
        Assert.That(cfg.Verbose, Is.True);
    }

    [Test]
    public void ShortFormArgs_Work()
    {
        var cfg = Parse("-r", "server", "-t", "t0", "-u", "direct|ep", "-v");
        Assert.That(cfg.Role, Is.EqualTo(Role.Server));
        Assert.That(cfg.Verbose, Is.True);
    }

    [Test]
    public void InvalidRole_Fails()
        => Assert.That(ParseOrNull("-r", "proxy", "-t", "t0", "-u", "url"), Is.Null);

    private static BridgeConfiguration? ParseOrNull(params string[] args)
        => CliParser.Parse(args, TextWriter.Null);
}
```

### `BridgeConfigurationTests`

```csharp
[TestFixture]
public class BridgeConfigurationTests
{
    [Test]
    public void DefaultBufferSize_Is10MB()
    {
        var cfg = new BridgeConfiguration();
        Assert.That(cfg.BufferSizeBytes, Is.EqualTo(10_485_760));
    }

    [Test]
    public void TunNames_DefaultsToEmpty()
    {
        var cfg = new BridgeConfiguration();
        Assert.That(cfg.TunNames, Is.Empty);
    }
}
```

## Success Criteria

1. All unit tests pass.
2. Running `PigeonPost --help` prints usage information without error.
3. Invalid arguments produce clear error messages on stderr.
4. Valid arguments produce a fully populated `BridgeConfiguration`.

## Files to Create/Modify

| File | Action |
|------|--------|
| `src/PigeonPost/Role.cs` | Create |
| `src/PigeonPost/BridgeConfiguration.cs` | Create |
| `src/PigeonPost/CliParser.cs` | Create |
| `tests/PigeonPost.Tests/CliParserTests.cs` | Create |
| `tests/PigeonPost.Tests/BridgeConfigurationTests.cs` | Create |
