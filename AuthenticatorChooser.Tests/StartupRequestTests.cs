using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class StartupRequestTests {

    [Fact]
    public void ToLaunchRequest_MapsFlags() {
        LaunchRequest request = Startup.ToLaunchRequest(true, true, true, 8, (true, "f.log"));
        request.Help.Should().BeTrue();
        request.AutostartOnLogon.Should().BeTrue();
        request.Cli.SkipAllNonSecurityKeyOptions.Should().BeTrue();
        request.Cli.AutoSubmitPinLength.Should().Be(8);
        request.Cli.FileLogEnabled.Should().BeTrue();
        request.Cli.LogFilename.Should().Be("f.log");
    }

}
