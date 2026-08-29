using System.Runtime.InteropServices;
using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class SilentUpdatePolicyTests {

    [Theory]
    [InlineData("v0.8.0", "0.8.0")]
    [InlineData("0.7.1", "0.7.1")]
    [InlineData("v1.0", "1.0")]
    public void TryParseTag_AcceptsReleaseTags(string tag, string expected) {
        SilentUpdatePolicy.TryParseTag(tag, out Version? version).Should().BeTrue();
        version.Should().Be(Version.Parse(expected));
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v")]
    [InlineData("nightly")]
    public void TryParseTag_RejectsNonVersions(string tag) {
        SilentUpdatePolicy.TryParseTag(tag, out _).Should().BeFalse();
    }

    [Fact]
    public void IsNewer_ComparesThreePartVersions() {
        SilentUpdatePolicy.IsNewer(Version.Parse("0.7.0"), Version.Parse("0.7.1")).Should().BeTrue();
        SilentUpdatePolicy.IsNewer(Version.Parse("0.7.0"), Version.Parse("0.7.0")).Should().BeFalse();
        SilentUpdatePolicy.IsNewer(Version.Parse("0.8.0"), Version.Parse("0.7.9")).Should().BeFalse();
    }

    [Theory]
    [InlineData("testhost", false)]
    [InlineData("testhost.net48", false)]
    [InlineData("vstest.console", false)]
    [InlineData("AuthenticatorChooser", true)]
    public void AllowsBackgroundProcess_SkipsTestHosts(string name, bool allowed) {
        SilentUpdatePolicy.AllowsBackgroundProcess(name).Should().Be(allowed);
    }

    [Fact]
    public void IsSafeToRestart_FalseOnlyWhileFidoActive() {
        SilentUpdatePolicy.IsSafeToRestart().Should().BeTrue();
        using (FidoActivity.Begin()) {
            SilentUpdatePolicy.IsSafeToRestart().Should().BeFalse();
        }

        SilentUpdatePolicy.IsSafeToRestart().Should().BeTrue();
    }

    [Fact]
    public void RidFor_SupportsPackagedArchitectures() {
        SilentUpdatePolicy.RidFor(Architecture.X64).Should().Be("win-x64");
        SilentUpdatePolicy.RidFor(Architecture.Arm64).Should().Be("win-arm64");
        SilentUpdatePolicy.RidFor(Architecture.X86).Should().BeNull();
    }

    [Fact]
    public void SetupFileName_MatchesReleaseAssets() {
        SilentUpdatePolicy.SetupFileName("win-x64").Should().Be("AuthenticatorChooser-Setup-win-x64.exe");
        SilentUpdatePolicy.SetupFileName("win-arm64").Should().Be("AuthenticatorChooser-Setup-win-arm64.exe");
    }

    [Fact]
    public void ShouldPoll_RespectsInterval() {
        DateTime now = new(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
        SilentUpdatePolicy.ShouldPoll(null, now, TimeSpan.FromHours(24)).Should().BeTrue();
        SilentUpdatePolicy.ShouldPoll(now.AddHours(-25), now, TimeSpan.FromHours(24)).Should().BeTrue();
        SilentUpdatePolicy.ShouldPoll(now.AddHours(-1), now, TimeSpan.FromHours(24)).Should().BeFalse();
    }

    [Fact]
    public void IsSafeSetupPath_RequiresKnownNameUnderTemp() {
        string root = Path.Combine(Path.GetTempPath(), "AuthenticatorChooserSilentTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string ok = Path.Combine(root, "AuthenticatorChooser-Setup-win-x64.exe");
            SilentUpdatePolicy.IsSafeSetupPath(ok, root).Should().BeTrue();
            SilentUpdatePolicy.IsSafeSetupPath(Path.Combine(root, "evil.exe"), root).Should().BeFalse();
            SilentUpdatePolicy.IsSafeSetupPath(Path.Combine(root, "..", "AuthenticatorChooser-Setup-win-x64.exe"), root).Should().BeFalse();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void HasInnoUninstaller_DetectsUnins000() {
        string root = Path.Combine(Path.GetTempPath(), "AuthenticatorChooserSilentTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            SilentUpdatePolicy.HasInnoUninstaller(root).Should().BeFalse();
            File.WriteAllText(Path.Combine(root, "unins000.exe"), "x");
            SilentUpdatePolicy.HasInnoUninstaller(root).Should().BeTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

}
