using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class InstallerScriptTests {

    [Fact]
    public void SetupDoesNotUseRestartManagerCloseDialog() {
        string script = File.ReadAllText(FindInstallerScript());
        script.Should().Contain("CloseApplications=no");
        script.Should().Contain("RestartApplications=no");
        script.Should().Contain("RestartIfNeededByRun=no");
        script.Should().Contain("PrepareToInstall");
        script.Should().Contain("taskkill.exe");
        script.Should().Contain("TaskKillApp");
        script.Should().NotContain("CloseApplications=yes");
    }

    [Fact]
    public void SilentSetupRelaunchesTheAppAfterInstall() {
        string script = File.ReadAllText(FindInstallerScript());
        script.Should().Contain("Flags: nowait postinstall skipifsilent");
        script.Should().Contain("Flags: nowait skipifnotsilent");
    }

    private static string FindInstallerScript() {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null) {
            string iss = Path.Combine(dir, "installer", "AuthenticatorChooser.iss");
            if (File.Exists(iss)) {
                return iss;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("installer/AuthenticatorChooser.iss was not found.");
    }

}
