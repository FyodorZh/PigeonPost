using NUnit.Framework;
using PigeonPost.Vpn;
using PigeonPost.VpnClientView.ViewModels;

namespace PigeonPost.VpnClientView.Tests;

[TestFixture]
public sealed class MainViewModelTests
{
    private static ConfigViewModel CreateConfigVm() => new();
    private static ConfigViewModel CreateConfigVmWithProfile()
    {
        var store = new TestProfileStore(new VpnProfile("tcp|203.0.113.10:9000/30", 15));
        return new ConfigViewModel(store);
    }
    private static DashboardViewModel CreateDashboardVm()
    {
        return new DashboardViewModel(new FakeVpnRuntime());
    }
    private static LogsViewModel CreateLogsVm()
    {
        return new LogsViewModel(new FakeVpnRuntime());
    }

    [Test]
    public void SelectedTabIndex_DefaultsToConfig_WhenNoProfile()
    {
        var vm = new MainViewModel(CreateConfigVm(), CreateDashboardVm(), CreateLogsVm());
        Assert.That(vm.SelectedTabIndex, Is.EqualTo(1));
    }

    [Test]
    public void SelectedTabIndex_DefaultsToDashboard_WhenProfileExists()
    {
        var vm = new MainViewModel(CreateConfigVmWithProfile(), CreateDashboardVm(), CreateLogsVm());
        Assert.That(vm.SelectedTabIndex, Is.EqualTo(0));
    }

    [Test]
    public void SetSelectedTabIndex_UpdatesProperty()
    {
        var vm = new MainViewModel(CreateConfigVm(), CreateDashboardVm(), CreateLogsVm());
        vm.SelectedTabIndex = 2;
        Assert.That(vm.SelectedTabIndex, Is.EqualTo(2));
    }

    [Test]
    public void PropertyChanged_RaisesOnTabChange()
    {
        var vm = new MainViewModel(CreateConfigVm(), CreateDashboardVm(), CreateLogsVm());
        var changed = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.SelectedTabIndex))
                changed = true;
        };
        vm.SelectedTabIndex = 3;
        Assert.That(changed, Is.True);
    }

    [Test]
    public void ConfigViewModel_IsAccessible()
    {
        var configVm = CreateConfigVm();
        var vm = new MainViewModel(configVm, CreateDashboardVm(), CreateLogsVm());
        Assert.That(vm.ConfigViewModel, Is.SameAs(configVm));
    }

    [Test]
    public void DashboardViewModel_IsAccessible()
    {
        var dashVm = CreateDashboardVm();
        var vm = new MainViewModel(CreateConfigVm(), dashVm, CreateLogsVm());
        Assert.That(vm.DashboardViewModel, Is.SameAs(dashVm));
    }

    [Test]
    public void LogsViewModel_IsAccessible()
    {
        var logsVm = CreateLogsVm();
        var vm = new MainViewModel(CreateConfigVm(), CreateDashboardVm(), logsVm);
        Assert.That(vm.LogsViewModel, Is.SameAs(logsVm));
    }
}
