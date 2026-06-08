using System;
using NUnit.Framework;

namespace PigeonPost.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class ReconnectionTests
{
    [Test]
    public void ClientReconnects_AfterServerRestart()
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("Requires Linux");

        // Start server
        // Connect client
        // Stop server
        // Verify client disconnects
        // Restart server
        // Verify client reconnects
        // Send packet through new connection
    }
}
