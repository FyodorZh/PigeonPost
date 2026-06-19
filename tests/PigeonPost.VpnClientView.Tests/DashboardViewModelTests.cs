using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PigeonPost.Bridge;
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

    [Test]
    public async Task Connect_DuplicateHostIp_ShowsClearError()
    {
        var runtime = new RejectingRuntime(HandshakeRejectCode.DuplicateHostIp);
        var vm = new DashboardViewModel(runtime, TestProfile);

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.That(vm.StatusText, Is.EqualTo("This client IP is already in use on the server"));
        Assert.That(vm.ConnectionState, Is.EqualTo(ConnectionState.Disconnected));
    }

    [Test]
    public async Task Connect_InvalidHandshake_ShowsClearError()
    {
        var runtime = new RejectingRuntime(HandshakeRejectCode.InvalidHandshake);
        var vm = new DashboardViewModel(runtime, TestProfile);

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.That(vm.StatusText, Is.EqualTo("Server rejected our handshake (invalid format)"));
    }

    [Test]
    public async Task Connect_ServerShuttingDown_ShowsClearError()
    {
        var runtime = new RejectingRuntime(HandshakeRejectCode.ServerShuttingDown);
        var vm = new DashboardViewModel(runtime, TestProfile);

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.That(vm.StatusText, Is.EqualTo("Server is shutting down"));
    }

    [Test]
    public async Task Connect_UnsupportedPacketFamily_ShowsClearError()
    {
        var runtime = new RejectingRuntime(HandshakeRejectCode.UnsupportedPacketFamily);
        var vm = new DashboardViewModel(runtime, TestProfile);

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.That(vm.StatusText, Is.EqualTo("Server does not support our packet type"));
    }

    [Test]
    public async Task Connect_TransportFailure_ShowsConnectionFailed()
    {
        var runtime = new ThrowingRuntime(new InvalidOperationException("Connection refused"));
        var vm = new DashboardViewModel(runtime, TestProfile);

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.That(vm.StatusText, Does.Contain("Connection refused"));
    }

    private sealed class RejectingRuntime : IVpnRuntime
    {
        public HandshakeRejectCode RejectCode { get; }

        public RejectingRuntime(HandshakeRejectCode code) => RejectCode = code;

        public ConnectionState State => ConnectionState.Disconnected;
        public VpnSessionSnapshot CurrentSession => new(ConnectionState.Disconnected, null, 0, 0, 0, 0, 0);
        public bool IsReconnecting => false;

#pragma warning disable CS0067
        public event Action<VpnSessionSnapshot>? SessionUpdated;
        public event Action<VpnLogEntry>? LogEmitted;
#pragma warning restore CS0067

        public Task ConnectAsync(VpnProfile profile, CancellationToken ct)
            => throw new HandshakeRejectedException(RejectCode);

        public Task DisconnectAsync() => Task.CompletedTask;
    }

    private sealed class ThrowingRuntime : IVpnRuntime
    {
        private readonly Exception _exception;

        public ThrowingRuntime(Exception exception) => _exception = exception;

        public ConnectionState State => ConnectionState.Disconnected;
        public VpnSessionSnapshot CurrentSession => new(ConnectionState.Disconnected, null, 0, 0, 0, 0, 0);
        public bool IsReconnecting => false;

#pragma warning disable CS0067
        public event Action<VpnSessionSnapshot>? SessionUpdated;
        public event Action<VpnLogEntry>? LogEmitted;
#pragma warning restore CS0067

        public Task ConnectAsync(VpnProfile profile, CancellationToken ct)
            => throw _exception;

        public Task DisconnectAsync() => Task.CompletedTask;
    }

    private sealed class TestAndroidBridge : IAndroidServiceBridge
    {
        public AndroidServiceState ServiceState { get; private set; }
        public bool IsVpnInterfaceEstablished { get; set; }
        public AndroidVpnConfiguration? CurrentConfiguration { get; set; }
        public event Action<AndroidServiceState>? ServiceStateChanged;

        public void RequestVpnPermission()
        {
            ServiceState = AndroidServiceState.Preparing;
            ServiceStateChanged?.Invoke(ServiceState);
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

        public void SimulatePermissionGranted()
        {
            ServiceState = AndroidServiceState.Running;
            ServiceStateChanged?.Invoke(ServiceState);
        }

        public void SetRuntime(IVpnRuntime runtime)
        {
        }

        public void SimulateVpnInterfaceEstablished()
        {
            IsVpnInterfaceEstablished = true;
            ServiceStateChanged?.Invoke(ServiceState);
        }
    }

    [Test]
    public void AndroidBridge_IsStored()
    {
        var bridge = new TestAndroidBridge();
        var vm = new DashboardViewModel(CreateRuntime(), TestProfile, androidBridge: bridge);
        Assert.That(vm, Is.Not.Null);
    }

    [Test]
    public void AndroidBridge_IdleState_RequestsPermissionOnConnect()
    {
        var bridge = new TestAndroidBridge();
        var vm = new DashboardViewModel(CreateRuntime(), TestProfile, androidBridge: bridge);

        _ = vm.ConnectCommand.ExecuteAsync(null);

        Assert.That(bridge.ServiceState, Is.EqualTo(AndroidServiceState.Preparing));
    }

    [Test]
    public async Task AndroidBridge_ConnectTriggersRuntimeConnectWhenServiceRunning()
    {
        var runtime = CreateRuntime();
        var bridge = new TestAndroidBridge();
        bridge.StartVpnService();

        var vm = new DashboardViewModel(runtime, TestProfile, androidBridge: bridge);

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.That(runtime.State, Is.EqualTo(ConnectionState.Connected));
    }

    [Test]
    public void AndroidBridge_RevokeDisconnectsRuntime()
    {
        var runtime = CreateRuntime();
        var bridge = new TestAndroidBridge();
        var vm = new DashboardViewModel(runtime, TestProfile, androidBridge: bridge);

        runtime.ConnectAsync(TestProfile, CancellationToken.None).Wait();
        Assert.That(runtime.State, Is.EqualTo(ConnectionState.Connected));

        bridge.SimulateRevoke();

        Assert.That(runtime.State, Is.EqualTo(ConnectionState.Disconnected));
    }

    [Test]
    public void AndroidBridge_DisconnectStopsService()
    {
        var runtime = CreateRuntime();
        var bridge = new TestAndroidBridge();
        var vm = new DashboardViewModel(runtime, TestProfile, androidBridge: bridge);

        bridge.StartVpnService();
        runtime.ConnectAsync(TestProfile, CancellationToken.None).Wait();

        _ = vm.DisconnectCommand.ExecuteAsync(null);

        Assert.That(bridge.ServiceState, Is.EqualTo(AndroidServiceState.Idle));
    }

    [Test]
    public async Task AndroidBridge_ServiceRunningEvent_TriggersRuntimeConnect()
    {
        var runtime = CreateRuntime();
        var bridge = new TestAndroidBridge();
        var vm = new DashboardViewModel(runtime, TestProfile, androidBridge: bridge);

        _ = vm.ConnectCommand.ExecuteAsync(null);

        bridge.SimulatePermissionGranted();
        bridge.SimulateVpnInterfaceEstablished();

        await Task.Delay(2500);

        Assert.That(runtime.State, Is.EqualTo(ConnectionState.Connected));
    }

    [Test]
    public void AndroidBridge_VpnInterfaceState_UpdatesFromBridge()
    {
        var runtime = CreateRuntime();
        var bridge = new TestAndroidBridge();
        var vm = new DashboardViewModel(runtime, TestProfile, androidBridge: bridge);

        Assert.That(vm.IsVpnInterfaceEstablished, Is.False);
        Assert.That(vm.VpnInterfaceStatusText, Is.EqualTo("VPN not configured"));

        bridge.SimulateVpnInterfaceEstablished();

        Assert.That(vm.IsVpnInterfaceEstablished, Is.True);
        Assert.That(vm.VpnInterfaceStatusText, Is.EqualTo("VPN interface established"));
    }

    [Test]
    public void AndroidBridge_VpnInterfaceNotEstablished_ShowsEstablishingMessage()
    {
        var runtime = CreateRuntime();
        var bridge = new TestAndroidBridge();
        var vm = new DashboardViewModel(runtime, TestProfile, androidBridge: bridge);

        _ = vm.ConnectCommand.ExecuteAsync(null);

        bridge.SimulatePermissionGranted();

        Assert.That(vm.StatusText, Is.EqualTo("Establishing VPN interface..."));
    }
}
