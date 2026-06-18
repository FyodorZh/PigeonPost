using NUnit.Framework;
using PigeonPost.VpnClientView.ViewModels;

namespace PigeonPost.VpnClientView.Tests;

[TestFixture]
public sealed class MainViewModelTests
{
    private static ConfigViewModel CreateConfigVm() => new();

    [Test]
    public void DefaultTabIndex_IsZero()
    {
        var vm = new MainViewModel(CreateConfigVm());
        Assert.That(vm.DefaultTabIndex, Is.EqualTo(0));
    }

    [Test]
    public void SelectedTabIndex_DefaultsToZero()
    {
        var vm = new MainViewModel(CreateConfigVm());
        Assert.That(vm.SelectedTabIndex, Is.EqualTo(0));
    }

    [Test]
    public void SetSelectedTabIndex_UpdatesProperty()
    {
        var vm = new MainViewModel(CreateConfigVm());
        vm.SelectedTabIndex = 2;
        Assert.That(vm.SelectedTabIndex, Is.EqualTo(2));
    }

    [Test]
    public void PropertyChanged_RaisesOnTabChange()
    {
        var vm = new MainViewModel(CreateConfigVm());
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
        var vm = new MainViewModel(configVm);
        Assert.That(vm.ConfigViewModel, Is.SameAs(configVm));
    }
}
