using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PigeonPost.Vpn;

public static partial class VpnProfileValidator
{
    [GeneratedRegex(@"^\w+\|\S+:\d+/\d+$", RegexOptions.Compiled)]
    private static partial Regex UrlPattern();

    public static List<string> Validate(string serverUrl, int clientIpLastOctet)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            errors.Add("Server URL is required.");
        }
        else if (!UrlPattern().IsMatch(serverUrl))
        {
            errors.Add("Server URL must match format: type|host:port/timeout (e.g., tcp|203.0.113.10:9000/30).");
        }

        if (clientIpLastOctet < 11)
        {
            errors.Add("Client IP last octet must be at least 11.");
        }
        else if (clientIpLastOctet > 254)
        {
            errors.Add("Client IP last octet must be at most 254.");
        }

        return errors;
    }
}
