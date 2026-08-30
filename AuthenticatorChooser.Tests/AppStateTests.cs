using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class AppStateTests {

    [Fact]
    public void FromSettings_AndToSettings_RoundTrip() {
        AppSettings settings = new() {
            SchemaVersion = AppSettings.CurrentSchema,
            Enabled = false,
            SkipAllNonSecurityKeyOptions = true,
            AutoSubmitPinLength = 6,
            LearnedPinLength = 10,
            FileLogEnabled = true,
            LogFilename = "a.log",
            AutostartOnLogon = true,
            TrayHintShown = true,
            AutoUpdateEnabled = false,
            LastUpdateCheckUtc = new DateTime(2026, 8, 29, 17, 0, 0, DateTimeKind.Utc)
        };
        AppState state = AppState.FromSettings(settings);
        AppSettings round = state.ToSettings();
        round.Enabled.Should().BeFalse();
        round.SkipAllNonSecurityKeyOptions.Should().BeTrue();
        round.AutoSubmitPinLength.Should().Be(6);
        round.LearnedPinLength.Should().Be(10);
        round.FileLogEnabled.Should().BeTrue();
        round.LogFilename.Should().Be("a.log");
        round.AutostartOnLogon.Should().BeTrue();
        round.TrayHintShown.Should().BeTrue();
        round.AutoUpdateEnabled.Should().BeFalse();
        round.LastUpdateCheckUtc.Should().Be(settings.LastUpdateCheckUtc);
        round.SchemaVersion.Should().Be(AppSettings.CurrentSchema);
        round.PinMode.Should().Be(PinMode.Off);
        round.PriorityRules.Should().HaveCount(3);
    }

    [Fact]
    public void Report_RaisesChanged() {
        AppState state = new();
        int count = 0;
        state.Changed += (_, _) => count++;
        state.Report(ChooserEventKind.Paused, "Paused, not submitting dialog box");
        state.LastEvent.Should().Be(ChooserEventKind.Paused);
        state.LastEventDetail.Should().Contain("Paused");
        count.Should().Be(1);
    }

    [Fact]
    public void ToggleEnabled_ViaPresenter() {
        AppState state = AppState.FromSettings(new AppSettings());
        StatusPresenter.ToggleEnabled(state);
        state.Enabled.Should().BeFalse();
        StatusPresenter.StatusLabel(state.Enabled).Should().Be("Paused");
        StatusPresenter.PauseActionLabel(state.Enabled).Should().Be("Resume");
    }

    [Fact]
    public void ChooserOptions_ReadsLiveState() {
        AppState state = new();
        ChooserOptions options = new(state);
        options.enabled.Should().BeTrue();
        state.SkipAllNonSecurityKeyOptions = true;
        options.skipAllNonSecurityKeyOptions.Should().BeTrue();
        state.AutoSubmitPinLength = 6;
        options.autoSubmitPinLength.Should().Be(6);
        state.LearnedPinLength = 10;
        options.learnedPinLength.Should().Be(10);
        PinCacheUxPolicy.SubmitLength(PinMode.Cache, options.autoSubmitPinLength, options.learnedPinLength).Should().Be(10);
        options.skipAllNonSecurityKeyOptions.Should().BeTrue();
        options.wantsAggressiveTitles.Should().BeTrue();
        state.SkipAllNonSecurityKeyOptions = false;
        state.PinMode = PinMode.Off;
        options.pinMode.Should().Be(PinMode.Off);
        options.wantsAggressiveTitles.Should().BeFalse();
        state.PinMode = PinMode.Cache;
        options.pinMode.Should().Be(PinMode.Cache);
        options.priorityRules.Should().HaveCount(3);
        options.wantsAggressiveTitles.Should().BeTrue();
    }

    [Fact]
    public void EventLabel_ReturnsDetailForEveryKind() {
        foreach (ChooserEventKind kind in Enum.GetValues<ChooserEventKind>()) {
            StatusPresenter.EventLabel(kind, "detail").Should().Be("detail");
        }
    }

}
