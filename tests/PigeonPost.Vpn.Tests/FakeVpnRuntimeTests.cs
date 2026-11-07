using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace PigeonPost.Vpn.Tests;

[TestFixture]
public sealed class FakeVpnRuntimeTests
{
    private static readonly VpnProfile TestProfile = new("tcp|203.0.113.10:9000/30", 15);

    [Test]
    public async Task Connect_TransitionsToConnected()
    {
        using var runtime = new FakeVpnRuntime();
        Assert.That(runtime.State, Is.EqualTo(ConnectionState.Disconnected));

        await runtime.ConnectAsync(TestProfile, CancellationToken.None);

        Assert.That(runtime.State, Is.EqualTo(ConnectionState.Connected));
        Assert.That(runtime.CurrentSession.SessionStart, Is.Not.Null);
    }

    [Test]
    public async Task Connect_EmitsConnectingEvent()
    {
        using var runtime = new FakeVpnRuntime();
        VpnSessionSnapshot? connectingSnapshot = null;
        runtime.SessionUpdated += snapshot =>
        {
            if (snapshot.State == ConnectionState.Connecting)
                connectingSnapshot = snapshot;
        };

        await runtime.ConnectAsync(TestProfile, CancellationToken.None);

        Assert.That(connectingSnapshot, Is.Not.Null);
        Assert.That(connectingSnapshot!.State, Is.EqualTo(ConnectionState.Connecting));
    }

    [Test]
    public async Task Connect_EmitsConnectedEvent()
    {
        using var runtime = new FakeVpnRuntime();
        VpnSessionSnapshot? connectedSnapshot = null;
        runtime.SessionUpdated += snapshot =>
        {
            if (snapshot.State == ConnectionState.Connected)
                connectedSnapshot = snapshot;
        };

        await runtime.ConnectAsync(TestProfile, CancellationToken.None);

        Assert.That(connectedSnapshot, Is.Not.Null);
        Assert.That(connectedSnapshot!.State, Is.EqualTo(ConnectionState.Connected));
    }

    [Test]
    public async Task Disconnect_TransitionsToDisconnected()
    {
        using var runtime = new FakeVpnRuntime();
        await runtime.ConnectAsync(TestProfile, CancellationToken.None);
        Assert.That(runtime.State, Is.EqualTo(ConnectionState.Connected));

        await runtime.DisconnectAsync();

        Assert.That(runtime.State, Is.EqualTo(ConnectionState.Disconnected));
        Assert.That(runtime.IsReconnecting, Is.False);
    }

    [Test]
    public async Task Disconnect_EmitsDisconnectedEvent()
    {
        using var runtime = new FakeVpnRuntime();
        await runtime.ConnectAsync(TestProfile, CancellationToken.None);

        VpnSessionSnapshot? disconnectedSnapshot = null;
        runtime.SessionUpdated += snapshot =>
        {
            if (snapshot.State == ConnectionState.Disconnected)
                disconnectedSnapshot = snapshot;
        };

        await runtime.DisconnectAsync();

        Assert.That(disconnectedSnapshot, Is.Not.Null);
        Assert.That(disconnectedSnapshot!.State, Is.EqualTo(ConnectionState.Disconnected));
    }

    [Test]
    public async Task DoubleConnect_IsNoOp()
    {
        using var runtime = new FakeVpnRuntime();
        await runtime.ConnectAsync(TestProfile, CancellationToken.None);
        Assert.That(runtime.State, Is.EqualTo(ConnectionState.Connected));

        await runtime.ConnectAsync(TestProfile, CancellationToken.None);

        Assert.That(runtime.State, Is.EqualTo(ConnectionState.Connected));
    }

    [Test]
    public async Task UnexpectedDisconnect_TriggersReconnect()
    {
        using var runtime = new FakeVpnRuntime();
        await runtime.ConnectAsync(TestProfile, CancellationToken.None);
        Assert.That(runtime.State, Is.EqualTo(ConnectionState.Connected));

        runtime.TestForceUnexpectedDisconnect();

        Assert.That(runtime.State, Is.EqualTo(ConnectionState.Disconnected));
        Assert.That(runtime.IsReconnecting, Is.True);

        await Task.Delay(2500);

        Assert.That(runtime.State, Is.EqualTo(ConnectionState.Connected));
        Assert.That(runtime.CurrentSession.ReconnectCount, Is.EqualTo(1));
    }

    [Test]
    public async Task SessionCountersReset_OnNewConnection()
    {
        using var runtime = new FakeVpnRuntime();

        // First connection
        await runtime.ConnectAsync(TestProfile, CancellationToken.None);
        Assert.That(runtime.CurrentSession.BytesSent, Is.EqualTo(0));
        Assert.That(runtime.CurrentSession.BytesReceived, Is.EqualTo(0));

        await Task.Delay(2000);

        var bytesSent = runtime.CurrentSession.BytesSent;
        var bytesReceived = runtime.CurrentSession.BytesReceived;
        Assert.That(bytesSent, Is.GreaterThan(0));
        Assert.That(bytesReceived, Is.GreaterThan(0));

        // Reconnect
        await runtime.DisconnectAsync();
        await runtime.ConnectAsync(TestProfile, CancellationToken.None);

        Assert.That(runtime.CurrentSession.BytesSent, Is.EqualTo(0));
        Assert.That(runtime.CurrentSession.BytesReceived, Is.EqualTo(0));
        Assert.That(runtime.CurrentSession.ReconnectCount, Is.EqualTo(0));
    }

    [Test]
    public async Task CurrentSession_UpdatesWithTraffic()
    {
        using var runtime = new FakeVpnRuntime();
        await runtime.ConnectAsync(TestProfile, CancellationToken.None);

        await Task.Delay(1500);

        var snapshot = runtime.CurrentSession;
        Assert.That(snapshot.BytesSent, Is.GreaterThan(0));
        Assert.That(snapshot.BytesReceived, Is.GreaterThan(0));
        Assert.That(snapshot.SpeedSentBps, Is.GreaterThan(0));
        Assert.That(snapshot.SpeedReceivedBps, Is.GreaterThan(0));
    }

    [Test]
    public async Task LogEmitted_FiresOnStateTransition()
    {
        using var runtime = new FakeVpnRuntime();
        var logCount = 0;
        runtime.LogEmitted += _ => logCount++;

        await runtime.ConnectAsync(TestProfile, CancellationToken.None);

        Assert.That(logCount, Is.GreaterThanOrEqualTo(1));
    }
}
