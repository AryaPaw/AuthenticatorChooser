using AuthenticatorChooser.Windows11;
using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class OsVersionTests {

    [Fact]
    public void UsesWmiWhenAvailable() {
        OsVersion version = OsVersion.getCurrent(new StubOsInfo(
            new OsWmiData("Microsoft Windows 11 Pro", "10.0.26100"),
            "25H2",
            3775,
            "AMD64"));
        version.name.Should().Be("Microsoft Windows 11 Pro");
        version.marketingVersion.Should().Be("25H2");
        version.architecture.Should().Be("AMD64");
        version.version.Revision.Should().Be(3775);
    }

    [Fact]
    public void FallsBackWhenWmiMissing() {
        OsVersion version = OsVersion.getCurrent(new StubOsInfo(null, null, 0, "ARM64"));
        version.name.Should().Be("Microsoft Windows");
        version.architecture.Should().Be("ARM64");
        version.marketingVersion.Should().BeEmpty();
    }

    private sealed class StubOsInfo(OsWmiData? wmi, string? display, int ubr, string arch): IOperatingSystemInfo {

        public OsWmiData? QueryWmi() => wmi;

        public string? RegistryDisplayVersion() => display;

        public int RegistryUbr() => ubr;

        public string Architecture() => arch;

    }

}
