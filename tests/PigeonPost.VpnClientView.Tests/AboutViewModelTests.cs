using System.Reflection;
using NUnit.Framework;
using PigeonPost.VpnClientView.ViewModels;

namespace PigeonPost.VpnClientView.Tests;

[TestFixture]
public sealed class AboutViewModelTests
{
    [Test]
    public void Version_IsNotEmpty()
    {
        var vm = new AboutViewModel();
        Assert.That(vm.Version, Is.Not.Empty);
    }

    [Test]
    public void ProductName_IsNotEmpty()
    {
        var vm = new AboutViewModel();
        Assert.That(vm.ProductName, Is.Not.Empty);
    }

    [Test]
    public void Description_IsNotEmpty()
    {
        var vm = new AboutViewModel();
        Assert.That(vm.Description, Is.Not.Empty);
    }

    [Test]
    public void BuildDate_IsNotNull()
    {
        var vm = new AboutViewModel();
        Assert.That(vm.BuildDate, Is.Not.Null);
    }

    [Test]
    public void Version_MatchesAssemblyVersion()
    {
        var vm = new AboutViewModel();
        var assembly = typeof(AboutViewModel).Assembly;
        var expectedVersion = assembly.GetName()?.Version?.ToString() ?? "1.0.0";
        Assert.That(vm.Version, Is.EqualTo(expectedVersion));
    }

    [Test]
    public void ProductName_MatchesAssemblyProduct()
    {
        var vm = new AboutViewModel();
        var assembly = typeof(AboutViewModel).Assembly;
        var expected = assembly.GetCustomAttribute<System.Reflection.AssemblyProductAttribute>()?.Product ?? "PigeonPost VPN Client";
        Assert.That(vm.ProductName, Is.EqualTo(expected));
    }
}
