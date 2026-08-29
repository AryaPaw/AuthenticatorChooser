using FluentAssertions;
using NLog.Config;
using NLog.Targets;

namespace AuthenticatorChooser.Tests;

public sealed class LoggingTests {

    [Fact]
    public void ResolveLogPath_UsesAppDataWhenOmitted() {
        string root = @"C:\Users\Public\AuthenticatorChooser";
        Logging.ResolveLogPath(null, root).Should().Be(Path.GetFullPath(Path.Combine(root, "AuthenticatorChooser.log")));
    }

    [Fact]
    public void ResolveLogPath_RejectsNestedAndAbsolutePaths() {
        Action windows = () => Logging.ResolveLogPath(@"C:\Windows\System32\evil.log", SettingsStore.DefaultDirectory);
        windows.Should().Throw<InvalidOperationException>();
        Action nested = () => Logging.ResolveLogPath(@"sub\evil.log", SettingsStore.DefaultDirectory);
        nested.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("settings.json")]
    [InlineData("${tempdir}")]
    public void ResolveLogPath_RejectsDotDotAndReservedNames(string name) {
        Action act = () => Logging.ResolveLogPath(name, SettingsStore.DefaultDirectory);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResolveLogPath_AllowsBareFileNameUnderAppData() {
        string path = Logging.ResolveLogPath("chooser.log", @"C:\Users\Public\AuthenticatorChooser");
        path.Should().EndWith($"{Path.DirectorySeparatorChar}chooser.log");
        path.Should().Contain("AuthenticatorChooser");
    }

    [Fact]
    public void CreateConfiguration_AddsFileTargetWhenEnabled() {
        LoggingConfiguration config = Logging.CreateConfiguration(true, Path.Combine(Path.GetTempPath(), "ac-test.log"));
        config.AllTargets.OfType<FileTarget>().Should().ContainSingle();
        config.AllTargets.OfType<ConsoleTarget>().Should().ContainSingle();
    }

    [Fact]
    public void CreateConfiguration_OmitsFileTargetWhenDisabled() {
        Logging.CreateConfiguration(false, "unused.log").AllTargets.OfType<FileTarget>().Should().BeEmpty();
    }

}
