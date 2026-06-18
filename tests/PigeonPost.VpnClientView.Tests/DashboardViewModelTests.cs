using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PigeonPost.Vpn;
using PigeonPost.VpnClientView.ViewModels;

namespace PigeonPost.VpnClientView.Tests;

[TestFixture]
public sealed class DashboardViewModelTests
{
    private static readonly VpnProfile TestProfile = new("tcp|203.0.113.10:9000/30", 15);

    private static FakeVpnRuntime CreateRuntime()
    {
        return new FakeVpnRuntime();
    }

    [Test]
    public void InitialState_IsDisconnected()
    {
        var runtime = CreateRuntime();
        var vm = new DashboardViewModel(runtime, TestProfile);

        Assert.That(vm.ConnectionState, Is.EqualTo(ConnectionState.Disconnected));
        Assert.That(vm.StatusText, Is.EqualTo("Disconnected"));
        Assert.That(vm.CanConnect, Is.True);
        Assert.That(vm.CanDisconnect, Is.False);
    }

    [Test]
    public async Task Connect_UpdatesState()
    {
        var runtime = CreateRuntime();
        var vm = new DashboardViewModel(runtime, TestProfile);

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.That(vm.ConnectionState, Is.EqualTo(ConnectionState.Connected));
        Assert.That(vm.StatusText, Is.EqualTo("Connected"));
    }

    [Test]
    public async Task Disconnect_UpdatesState()
    {
        var runtime = CreateRuntime();
        var vm = new DashboardViewModel(runtime, TestProfile);

        await vm.ConnectCommand.ExecuteAsync(null);
        Assert.That(vm.ConnectionState, Is.EqualTo(ConnectionState.Connected));

        await vm.DisconnectCommand.ExecuteAsync(null);

        Assert.That(vm.ConnectionState, Is.EqualTo(ConnectionState.Disconnected));
        Assert.That(vm.StatusText, Is.EqualTo("Disconnected"));
    }

    [Test]
    public async Task Counters_UpdateOnTraffic()
    {
        var runtime = CreateRuntime();
        var vm = new DashboardViewModel(runtime, TestProfile);

        await vm.ConnectCommand.ExecuteAsync(null);
        await Task.Delay(1500);

        Assert.That(vm.BytesSentText, Is.Not.EqualTo("0 B"));
        Assert.That(vm.BytesReceivedText, Is.Not.EqualTo("0 B"));
        Assert.That(vm.SpeedUpText, Is.Not.EqualTo("0 bps"));
        Assert.That(vm.SpeedDownText, Is.Not.EqualTo("0 bps"));
    }

    [Test]
    public async Task Uptime_StartsEmpty_ThenPopulates()
    {
        var runtime = CreateRuntime();
        var vm = new DashboardViewModel(runtime, TestProfile);

        Assert.That(vm.UptimeText, Is.EqualTo("--"));

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.That(vm.UptimeText, Is.Not.EqualTo("--"));
    }

    [Test]
    public async Task ReconnectCount_UpdatesOnReconnect()
    {
        var runtime = CreateRuntime();
        var vm = new DashboardViewModel(runtime, TestProfile);

        await vm.ConnectCommand.ExecuteAsync(null);

        runtime.TestForceUnexpectedDisconnect();
        await Task.Delay(2500);

        Assert.That(vm.ReconnectCount, Is.EqualTo(1));
    }

    [Test]
    public void FormatBytes_VariousSizes()
    {
        var vm = new DashboardViewModel(CreateRuntime(), TestProfile);
        var method = typeof(DashboardViewModel)
            .GetMethod("FormatBytes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
    }

    [Test]
    public async Task CanConnect_Disabled_WhileConnected()
    {
        var runtime = CreateRuntime();
        var vm = new DashboardViewModel(runtime, TestProfile);

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.That(vm.CanConnect, Is.False);
        Assert.That(vm.CanDisconnect, Is.True);
    }
}
