using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PigeonPost.Vpn;
using PigeonPost.VpnClientView.ViewModels;

namespace PigeonPost.VpnClientView.Tests;

[TestFixture]
public sealed class ConfigViewModelTests
{
    [Test]
    public void DefaultState_Invalid()
    {
        var vm = new ConfigViewModel();
        Assert.That(vm.IsValid, Is.False);
        Assert.That(vm.Errors, Is.Not.Empty);
    }

    [Test]
    public void ValidInput_BecomesValid()
    {
        var vm = new ConfigViewModel();
        vm.ServerUrl = "tcp|203.0.113.10:9000/30";
        vm.ClientIpLastOctet = "15";
        Assert.That(vm.IsValid, Is.True);
        Assert.That(vm.Errors, Is.Empty);
    }

    [Test]
    public void InvalidOctet_ClearsValidState()
    {
        var vm = new ConfigViewModel();
        vm.ServerUrl = "tcp|203.0.113.10:9000/30";
        vm.ClientIpLastOctet = "15";
        Assert.That(vm.IsValid, Is.True);

        vm.ClientIpLastOctet = "3";
        Assert.That(vm.IsValid, Is.False);
        Assert.That(vm.Errors, Is.Not.Empty);
    }

    [Test]
    public void InvalidUrl_ClearsValidState()
    {
        var vm = new ConfigViewModel();
        vm.ServerUrl = "tcp|203.0.113.10:9000/30";
        vm.ClientIpLastOctet = "15";
        Assert.That(vm.IsValid, Is.True);

        vm.ServerUrl = "bad";
        Assert.That(vm.IsValid, Is.False);
    }

    [Test]
    public void FullIpPreview_UpdatesWithValidOctet()
    {
        var vm = new ConfigViewModel();
        vm.ClientIpLastOctet = "15";
        Assert.That(vm.FullIpPreview, Is.EqualTo("10.0.10.15"));
    }

    [Test]
    public void FullIpPreview_ShowsXForNonNumeric()
    {
        var vm = new ConfigViewModel();
        vm.ClientIpLastOctet = "abc";
        Assert.That(vm.FullIpPreview, Is.EqualTo("10.0.10.x"));
    }

    [Test]
    public void FullIpPreview_ShowsXForBelowRange()
    {
        var vm = new ConfigViewModel();
        vm.ClientIpLastOctet = "5";
        Assert.That(vm.FullIpPreview, Is.EqualTo("10.0.10.x"));
    }

    [Test]
    public void FullIpPreview_ShowsXForAboveRange()
    {
        var vm = new ConfigViewModel();
        vm.ClientIpLastOctet = "300";
        Assert.That(vm.FullIpPreview, Is.EqualTo("10.0.10.x"));
    }

    [Test]
    public void ErrorsCollection_ClearsOnRevalidation()
    {
        var vm = new ConfigViewModel();
        Assert.That(vm.Errors, Is.Not.Empty);

        vm.ServerUrl = "tcp|203.0.113.10:9000/30";
        vm.ClientIpLastOctet = "15";
        Assert.That(vm.Errors, Is.Empty);

        vm.ClientIpLastOctet = "1";
        Assert.That(vm.Errors, Is.Not.Empty);
    }

    [Test]
    public void LoadsProfile_WhenStoreHasProfile()
    {
        var store = new TestProfileStore(new VpnProfile("tcp|203.0.113.10:9000/30", 42));
        var vm = new ConfigViewModel(store: store);
        Assert.That(vm.HasLoadedProfile, Is.True);
        Assert.That(vm.ServerUrl, Is.EqualTo("tcp|203.0.113.10:9000/30"));
        Assert.That(vm.ClientIpLastOctet, Is.EqualTo("42"));
    }

    [Test]
    public void HasLoadedProfile_False_WhenStoreIsNull()
    {
        var vm = new ConfigViewModel();
        Assert.That(vm.HasLoadedProfile, Is.False);
    }

    [Test]
    public void HasLoadedProfile_False_WhenStoreReturnsNull()
    {
        var store = new TestProfileStore(null);
        var vm = new ConfigViewModel(store: store);
        Assert.That(vm.HasLoadedProfile, Is.False);
    }

    [Test]
    public void ReconnectWarning_Hidden_WhenNoRuntime()
    {
        var vm = new ConfigViewModel();
        Assert.That(vm.IsReconnectWarningVisible, Is.False);
    }

    [Test]
    public void ReconnectWarning_Visible_WhenRuntimeConnected()
    {
        using var runtime = new FakeVpnRuntime();
        var vm = new ConfigViewModel(runtime: runtime);
        Assert.That(vm.IsReconnectWarningVisible, Is.False);
    }

    [Test]
    public async Task ReconnectWarning_ShowsOnConnect()
    {
        using var runtime = new FakeVpnRuntime();
        var vm = new ConfigViewModel(runtime: runtime);
        Assert.That(vm.IsReconnectWarningVisible, Is.False);

        await runtime.ConnectAsync(new VpnProfile("tcp|203.0.113.10:9000/30", 15), CancellationToken.None);

        Assert.That(vm.IsReconnectWarningVisible, Is.True);
    }

    [Test]
    public async Task ReconnectWarning_HidesOnDisconnect()
    {
        using var runtime = new FakeVpnRuntime();
        var vm = new ConfigViewModel(runtime: runtime);

        await runtime.ConnectAsync(new VpnProfile("tcp|203.0.113.10:9000/30", 15), CancellationToken.None);
        Assert.That(vm.IsReconnectWarningVisible, Is.True);

        await runtime.DisconnectAsync();

        Assert.That(vm.IsReconnectWarningVisible, Is.False);
    }
}
