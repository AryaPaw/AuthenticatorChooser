using FluentAssertions;
using NSubstitute;

namespace AuthenticatorChooser.Tests;

public sealed class UninstallCleanupTests: IDisposable {

    private readonly string root;

    public UninstallCleanupTests() {
        root = Path.Combine(Path.GetTempPath(), "AuthenticatorChooserUninstall", Guid.NewGuid().ToString("N"), "AuthenticatorChooser");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(root, "AuthenticatorChooser.log"), "log");
    }

    [Fact]
    public void Execute_RemovesDataAndUnregistersAutostart() {
        IAutostartService autostart = Substitute.For<IAutostartService>();
        autostart.Unregister().Returns(true);
        bool runDeleted = false;
        UninstallCleanup.Execute(autostart, root, () => runDeleted = true).Should().BeTrue();
        Directory.Exists(root).Should().BeFalse();
        runDeleted.Should().BeTrue();
        autostart.Received(1).Unregister();
    }

    [Fact]
    public void Execute_RejectsPathOutsideAppFolderName() {
        IAutostartService autostart = Substitute.For<IAutostartService>();
        string other = Path.Combine(Path.GetTempPath(), "NotChooser-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "x.txt"), "nope");
        UninstallCleanup.Execute(autostart, other, () => { }).Should().BeFalse();
        File.Exists(Path.Combine(other, "x.txt")).Should().BeTrue();
        Directory.Delete(other, true);
        autostart.DidNotReceive().Unregister();
    }

    public void Dispose() {
        if (Directory.Exists(root)) {
            Directory.Delete(root, true);
        }
    }

}
