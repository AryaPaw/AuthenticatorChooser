using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class TrayIconTests {

    [Theory]
    [InlineData("AuthenticatorChooser", true)]
    [InlineData("testhost", false)]
    [InlineData("testhost.x86", false)]
    [InlineData("vstest.console", false)]
    [InlineData("VSTest.Console", false)]
    public void DesktopNotifications_SkipTestHosts(string processName, bool expected) {
        TrayIcon.AllowsDesktopNotifications(processName).Should().Be(expected);
    }

}
