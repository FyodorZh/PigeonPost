using NUnit.Framework;
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
}
