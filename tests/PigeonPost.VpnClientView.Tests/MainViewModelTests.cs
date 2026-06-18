using NUnit.Framework;
using PigeonPost.Vpn;
using PigeonPost.VpnClientView.ViewModels;

namespace PigeonPost.VpnClientView.Tests;

[TestFixture]
public sealed class MainViewModelTests
{
    private static AboutViewModel CreateAboutVm() => new();
    private static ConfigViewModel CreateConfigVm() => new();
    private static ConfigViewModel CreateConfigVmWithProfile()
    {
        var store = new TestProfileStore(new VpnProfile("tcp|203.0.113.10:9000/30", 15));
        return new ConfigViewModel(store: store);
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
        var vm = new MainViewModel(CreateConfigVm(), CreateDashboardVm(), CreateLogsVm(), CreateAboutVm());
        Assert.That(vm.SelectedTabIndex, Is.EqualTo(1));
    }

    [Test]
    public void SelectedTabIndex_DefaultsToDashboard_WhenProfileExists()
    {
        var vm = new MainViewModel(CreateConfigVmWithProfile(), CreateDashboardVm(), CreateLogsVm(), CreateAboutVm());
        Assert.That(vm.SelectedTabIndex, Is.EqualTo(0));
    }

    [Test]
    public void SetSelectedTabIndex_UpdatesProperty()
    {
        var vm = new MainViewModel(CreateConfigVm(), CreateDashboardVm(), CreateLogsVm(), CreateAboutVm());
        vm.SelectedTabIndex = 2;
        Assert.That(vm.SelectedTabIndex, Is.EqualTo(2));
    }

    [Test]
    public void PropertyChanged_RaisesOnTabChange()
    {
        var vm = new MainViewModel(CreateConfigVm(), CreateDashboardVm(), CreateLogsVm(), CreateAboutVm());
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
        var vm = new MainViewModel(configVm, CreateDashboardVm(), CreateLogsVm(), CreateAboutVm());
        Assert.That(vm.ConfigViewModel, Is.SameAs(configVm));
    }

    [Test]
    public void DashboardViewModel_IsAccessible()
    {
        var dashVm = CreateDashboardVm();
        var vm = new MainViewModel(CreateConfigVm(), dashVm, CreateLogsVm(), CreateAboutVm());
        Assert.That(vm.DashboardViewModel, Is.SameAs(dashVm));
    }

    [Test]
    public void LogsViewModel_IsAccessible()
    {
        var logsVm = CreateLogsVm();
        var vm = new MainViewModel(CreateConfigVm(), CreateDashboardVm(), logsVm, CreateAboutVm());
        Assert.That(vm.LogsViewModel, Is.SameAs(logsVm));
    }

    [Test]
    public void AboutViewModel_IsAccessible()
    {
        var aboutVm = CreateAboutVm();
        var vm = new MainViewModel(CreateConfigVm(), CreateDashboardVm(), CreateLogsVm(), aboutVm);
        Assert.That(vm.AboutViewModel, Is.SameAs(aboutVm));
    }

    [Test]
    public void UpdateLayout_WideLayout_WhenWidthGe700()
    {
        var vm = new MainViewModel(CreateConfigVm(), CreateDashboardVm(), CreateLogsVm(), CreateAboutVm());
        vm.UpdateLayout(800);
        Assert.That(vm.IsWideLayout, Is.True);
        Assert.That(vm.IsNarrowLayout, Is.False);
    }

    [Test]
    public void UpdateLayout_NarrowLayout_WhenWidthLt700()
    {
        var vm = new MainViewModel(CreateConfigVm(), CreateDashboardVm(), CreateLogsVm(), CreateAboutVm());
        vm.UpdateLayout(600);
        Assert.That(vm.IsWideLayout, Is.False);
        Assert.That(vm.IsNarrowLayout, Is.True);
    }

    [Test]
    public void SelectedTabChanged_UpdatesTabFlags()
    {
        var vm = new MainViewModel(CreateConfigVm(), CreateDashboardVm(), CreateLogsVm(), CreateAboutVm());
        Assert.That(vm.IsConfigTab, Is.True);

        vm.SelectedTabIndex = 0;
        Assert.That(vm.IsDashboardTab, Is.True);
        Assert.That(vm.IsConfigTab, Is.False);
        Assert.That(vm.IsLogsTab, Is.False);
        Assert.That(vm.IsAboutTab, Is.False);
    }
}
