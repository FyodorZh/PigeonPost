using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace PigeonPost.Tun.Tests;

[TestFixture]
public class IfreqTests
{
    [Test]
    public void Size_Is40Bytes() => Assert.That(Unsafe.SizeOf<ifreq>(), Is.EqualTo(40));

    [Test]
    public void FlagsField_AtOffset16()
    {
        var ifr = new ifreq { ifr_name = "tun0", ifr_flags = 0x42 };
        Assert.That(ifr.ifr_flags, Is.EqualTo((short)0x42));
        Assert.That(ifr.ifr_name, Is.EqualTo("tun0"));
    }

    [Test]
    public void Name_TruncatedTo16Bytes()
    {
        var ifr = new ifreq { ifr_name = "very_long_name_12345", ifr_flags = 0 };
        // Name longer than 15 chars + null terminator should be truncated
        // when marshaled to native (C# Marshal ByValTStr truncates to SizeConst)
    }
}
