using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class StartupRequestTests {

    [Fact]
    public void ToLaunchRequest_MapsFlags() {
        LaunchRequest request = Startup.ToLaunchRequest(true, true, true, 8, (true, "f.log"));
        request.Help.Should().BeTrue();
        request.AutostartOnLogon.Should().BeTrue();
        request.UninstallCleanup.Should().BeFalse();
        request.Cli.SkipAllNonSecurityKeyOptions.Should().BeTrue();
        request.Cli.AutoSubmitPinLength.Should().Be(8);
        request.Cli.FileLogEnabled.Should().BeTrue();
        request.Cli.LogFilename.Should().Be("f.log");
    }

    [Fact]
    public void ToLaunchRequest_MapsShowWindow() {
        Startup.ToLaunchRequest(false, false, false, null, (false, null), false, true).ShowWindow.Should().BeTrue();
        Startup.ToLaunchRequest(false, false, false, null, (false, null)).ShowWindow.Should().BeFalse();
    }

    [Fact]
    public void ToLaunchRequest_MapsUninstallCleanup() {
        Startup.ToLaunchRequest(false, false, false, null, (false, null), true).UninstallCleanup.Should().BeTrue();
    }

}
