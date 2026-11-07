using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PigeonPost.Vpn;
using PigeonPost.VpnClientView.ViewModels;

namespace PigeonPost.VpnClientView.Tests;

[TestFixture]
public sealed class LogsViewModelTests
{
    private static readonly VpnProfile TestProfile = new("tcp|203.0.113.10:9000/30", 15);

    [Test]
    public void LogsCollection_StartsEmpty()
    {
        using var runtime = new FakeVpnRuntime();
        var vm = new LogsViewModel(runtime);

        Assert.That(vm.Logs, Is.Empty);
    }

    [Test]
    public async Task LogsCollection_PopulatesOnConnect()
    {
        using var runtime = new FakeVpnRuntime();
        var vm = new LogsViewModel(runtime);

        await runtime.ConnectAsync(TestProfile, CancellationToken.None);

        Assert.That(vm.Logs, Is.Not.Empty);
        Assert.That(vm.Logs[0].Message, Is.EqualTo("Connecting..."));
        Assert.That(vm.Logs[0].Level, Is.EqualTo(VpnLogLevel.Info));
    }

    [Test]
    public async Task LogsContainDisconnectedMessage()
    {
        using var runtime = new FakeVpnRuntime();
        var vm = new LogsViewModel(runtime);

        await runtime.ConnectAsync(TestProfile, CancellationToken.None);
        await runtime.DisconnectAsync();

        var hasDisconnect = false;
        foreach (var entry in vm.Logs)
        {
            if (entry.Message == "Disconnected")
            {
                hasDisconnect = true;
                break;
            }
        }

        Assert.That(hasDisconnect, Is.True);
    }

    [Test]
    public async Task LogEntriesHaveTimestamps()
    {
        using var runtime = new FakeVpnRuntime();
        var vm = new LogsViewModel(runtime);

        await runtime.ConnectAsync(TestProfile, CancellationToken.None);

        foreach (var entry in vm.Logs)
        {
            Assert.That(entry.Timestamp, Is.Not.EqualTo(default(DateTime)));
        }
    }

    [Test]
    public async Task MultipleLogs_AllCollected()
    {
        using var runtime = new FakeVpnRuntime();
        var vm = new LogsViewModel(runtime);

        await runtime.ConnectAsync(TestProfile, CancellationToken.None);
        await runtime.DisconnectAsync();
        await runtime.ConnectAsync(TestProfile, CancellationToken.None);

        Assert.That(vm.Logs.Count, Is.GreaterThanOrEqualTo(3));
    }
}
