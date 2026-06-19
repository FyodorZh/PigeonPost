using System;
using NUnit.Framework;
using PigeonPost.Vpn;

namespace PigeonPost.Vpn.Tests;

[TestFixture]
public sealed class AndroidServiceStateTests
{
    [Test]
    public void Enum_HasIdleValue()
    {
        Assert.That((int)AndroidServiceState.Idle, Is.EqualTo(0));
    }

    [Test]
    public void Enum_HasPreparingValue()
    {
        Assert.That((int)AndroidServiceState.Preparing, Is.EqualTo(1));
    }

    [Test]
    public void Enum_HasRunningValue()
    {
        Assert.That((int)AndroidServiceState.Running, Is.EqualTo(2));
    }

    [Test]
    public void Enum_HasRevokedValue()
    {
        Assert.That((int)AndroidServiceState.Revoked, Is.EqualTo(3));
    }

    [Test]
    public void TestBridge_InitialStateIsIdle()
    {
        var bridge = new TestAndroidServiceBridge();
        Assert.That(bridge.ServiceState, Is.EqualTo(AndroidServiceState.Idle));
    }

    [Test]
    public void TestBridge_StartService_TransitionsToRunning()
    {
        var bridge = new TestAndroidServiceBridge();
        bridge.StartVpnService();
        Assert.That(bridge.ServiceState, Is.EqualTo(AndroidServiceState.Running));
    }

    [Test]
    public void TestBridge_StopService_TransitionsToIdle()
    {
        var bridge = new TestAndroidServiceBridge();
        bridge.StartVpnService();
        bridge.StopVpnService();
        Assert.That(bridge.ServiceState, Is.EqualTo(AndroidServiceState.Idle));
    }

    [Test]
    public void TestBridge_StartService_FiresStateChanged()
    {
        var bridge = new TestAndroidServiceBridge();
        AndroidServiceState? received = null;
        bridge.ServiceStateChanged += s => received = s;

        bridge.StartVpnService();

        Assert.That(received, Is.EqualTo(AndroidServiceState.Running));
    }

    [Test]
    public void TestBridge_StopService_FiresStateChanged()
    {
        var bridge = new TestAndroidServiceBridge();
        bridge.StartVpnService();
        AndroidServiceState? received = null;
        bridge.ServiceStateChanged += s => received = s;

        bridge.StopVpnService();

        Assert.That(received, Is.EqualTo(AndroidServiceState.Idle));
    }

    [Test]
    public void TestBridge_Revoke_TransitionsToRevoked()
    {
        var bridge = new TestAndroidServiceBridge();
        bridge.StartVpnService();
        bridge.SimulateRevoke();
        Assert.That(bridge.ServiceState, Is.EqualTo(AndroidServiceState.Revoked));
    }

    [Test]
    public void TestBridge_Revoke_FiresStateChanged()
    {
        var bridge = new TestAndroidServiceBridge();
        bridge.StartVpnService();
        AndroidServiceState? received = null;
        bridge.ServiceStateChanged += s => received = s;

        bridge.SimulateRevoke();

        Assert.That(received, Is.EqualTo(AndroidServiceState.Revoked));
    }

    [Test]
    public void TestBridge_StateTransitions_IdleToRevoked_NoIntermediate()
    {
        var bridge = new TestAndroidServiceBridge();
        bridge.SimulateRevoke();
        Assert.That(bridge.ServiceState, Is.EqualTo(AndroidServiceState.Revoked));
    }
}

internal sealed class TestAndroidServiceBridge : IAndroidServiceBridge
{
    public AndroidServiceState ServiceState { get; private set; }
    public event Action<AndroidServiceState>? ServiceStateChanged;

    public void RequestVpnPermission()
    {
    }

    public void StartVpnService()
    {
        ServiceState = AndroidServiceState.Running;
        ServiceStateChanged?.Invoke(ServiceState);
    }

    public void StopVpnService()
    {
        ServiceState = AndroidServiceState.Idle;
        ServiceStateChanged?.Invoke(ServiceState);
    }

    public void SimulateRevoke()
    {
        ServiceState = AndroidServiceState.Revoked;
        ServiceStateChanged?.Invoke(ServiceState);
    }
}
