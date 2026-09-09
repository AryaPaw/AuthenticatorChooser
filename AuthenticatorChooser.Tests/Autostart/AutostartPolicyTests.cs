using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class AutostartPolicyTests: IDisposable {

    private readonly string root;

    public AutostartPolicyTests() {
        root = Path.Combine(Path.GetTempPath(), "AuthenticatorChooserAutostartPolicy", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [Fact]
    public void CanOwnLogonTask_TrueOnlyNextToInnoUninstaller() {
        string portable = Path.Combine(root, "portable", "AuthenticatorChooser.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(portable)!);
        File.WriteAllText(portable, "x");
        AutostartPolicy.CanOwnLogonTask(portable).Should().BeFalse();
        AutostartPolicy.CanOwnLogonTask("").Should().BeFalse();

        string installed = Path.Combine(root, "installed", "AuthenticatorChooser.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
        File.WriteAllText(installed, "x");
        File.WriteAllText(Path.Combine(root, "installed", "unins000.exe"), "x");
        AutostartPolicy.CanOwnLogonTask(installed).Should().BeTrue();
    }

    public void Dispose() {
        if (Directory.Exists(root)) {
            Directory.Delete(root, true);
        }
    }

}
