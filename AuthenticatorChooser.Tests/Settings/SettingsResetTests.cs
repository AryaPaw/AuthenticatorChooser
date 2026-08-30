using FluentAssertions;
using NSubstitute;

namespace AuthenticatorChooser.Tests;

public sealed class SettingsResetTests: IDisposable {

    private readonly string root;
    private readonly string settingsPath;

    public SettingsResetTests() {
        root = Path.Combine(Path.GetTempPath(), "AuthenticatorChooserReset", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        settingsPath = Path.Combine(root, "settings.json");
    }

    [Fact]
    public void TryApply_WritesFactoryDefaultsAndRegistersAutostart() {
        AppState state = AppState.FromSettings(new AppSettings {
            Enabled = false,
            SkipAllNonSecurityKeyOptions = true,
            AutoSubmitPinLength = 8,
            LearnedPinLength = 10,
            FileLogEnabled = true,
            LogFilename = "debug.log",
            AutostartOnLogon = false,
            TrayHintShown = true,
            AutoUpdateEnabled = false,
            LastUpdateCheckUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        IAutostartService autostart = Substitute.For<IAutostartService>();
        autostart.Register(Arg.Any<string>(), Arg.Any<string?>()).Returns(true);

        SettingsReset.TryApply(state, autostart, Path.Combine(root, "app.exe"), settingsPath, root).Should().BeTrue();

        state.Enabled.Should().BeTrue();
        state.SkipAllNonSecurityKeyOptions.Should().BeFalse();
        state.AutoSubmitPinLength.Should().BeNull();
        state.LearnedPinLength.Should().BeNull();
        state.PinMode.Should().Be(PinMode.Off);
        state.PinCacheLifetime.Should().Be(PinCacheLifetime.TwoMinutes);
        AuthenticatorPriorityCatalog.ActionFor(state.PriorityRules, AuthenticatorPriorityCatalog.UsbId)
            .Should().Be(AuthenticatorRuleAction.Select);
        state.FileLogEnabled.Should().BeFalse();
        state.AutostartOnLogon.Should().BeTrue();
        state.TrayHintShown.Should().BeFalse();
        state.AutoUpdateEnabled.Should().BeTrue();
        state.LastUpdateCheckUtc.Should().BeNull();
        SettingsStore.Load(settingsPath).AutoSubmitPinLength.Should().Be(0);
        SettingsStore.Load(settingsPath).LearnedPinLength.Should().Be(0);
        autostart.Received(1).Register(Path.Combine(root, "app.exe"), null);
        autostart.DidNotReceive().Unregister();
    }

    public void Dispose() {
        if (Directory.Exists(root)) {
            Directory.Delete(root, true);
        }
    }

}
