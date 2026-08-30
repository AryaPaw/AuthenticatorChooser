using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class AppSessionFidoFirstTests {

    [Fact]
    public void RunUi_StartsFidoListenerBeforeSilentUpdate() {
        string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string path = Path.Combine(repo, "AuthenticatorChooser", "AppSession.cs");
        File.Exists(path).Should().BeTrue(path);
        string src = File.ReadAllText(path);
        src.Should().NotContain("TryApplyAtLogon");
        src.Should().NotContain("GetAwaiter().GetResult()");
        src.IndexOf("WindowOpeningListener", StringComparison.Ordinal).Should().BePositive();
        src.IndexOf("SilentUpdateRuntime.Start", StringComparison.Ordinal)
            .Should().BeGreaterThan(src.IndexOf("WindowOpeningListener", StringComparison.Ordinal));
        src.Should().Contain("SessionSwitch");
        src.Should().Contain("PowerModeChanged");
        src.Should().Contain("pinCache.Clear()");
    }

}
