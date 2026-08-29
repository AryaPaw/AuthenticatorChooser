using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class SilentSetupLauncherTests {

    [Fact]
    public void BuildCommand_WaitsThenRunsInnoSilent() {
        string setup = @"C:\Temp\AuthenticatorChooser\AuthenticatorChooser-Setup-win-x64.exe";
        string command = SilentSetupLauncher.BuildCommand(setup);
        command.Should().Contain("ping 127.0.0.1 -n 5");
        command.Should().Contain("/VERYSILENT");
        command.Should().Contain("/SUPPRESSMSGBOXES");
        command.Should().Contain("/NORESTART");
        command.Should().Contain("/FORCECLOSEAPPLICATIONS");
        command.Should().Contain("\"" + setup + "\"");
        command.Should().StartWith("/C ping 127.0.0.1 -n 5 >NUL & ");
    }

    [Fact]
    public void BuildCommand_RejectsUnsafePaths() {
        Action act = () => SilentSetupLauncher.BuildCommand(@"C:\Temp\setup.exe & notepad.exe");
        act.Should().Throw<ArgumentException>();
    }

}
