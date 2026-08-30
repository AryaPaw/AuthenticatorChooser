using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class Win11PolicyTests {

    [Fact]
    public void ListPolicy_SelectsUseAnotherDeviceOnTpmWhenSkipAll() {
        Win1123H2ListDecision decision = Win1123H2ListPolicy.Decide(false, true, true, false, SkipReason.None);
        decision.IsLocalWindowsHelloTpmPrompt.Should().BeTrue();
        decision.SelectChoice.Should().BeTrue();
        decision.TrySubmitNext.Should().BeFalse();
    }

    [Fact]
    public void ListPolicy_DoesNotSelectWhenPaused() {
        Win1123H2ListDecision paused = Win1123H2ListPolicy.Decide(true, false, false, false, SkipReason.Paused);
        paused.SelectChoice.Should().BeFalse();
        paused.TrySubmitNext.Should().BeFalse();
    }

    [Fact]
    public void ListPolicy_DoesNotSelectTpmChoiceWhenShiftHeld() {
        Win1123H2ListDecision decision = Win1123H2ListPolicy.Decide(false, true, true, true, SkipReason.ShiftHeld);
        decision.SelectChoice.Should().BeFalse();
    }

    [Fact]
    public void ListPolicy_SubmitsSecurityKeyWhenAllowed() {
        Win1123H2ListDecision decision = Win1123H2ListPolicy.Decide(true, false, false, false, SkipReason.None);
        decision.TrySubmitNext.Should().BeTrue();
        decision.SelectChoice.Should().BeTrue();
    }

    [Fact]
    public void ListPolicy_MissingChoice() {
        Win1123H2ListPolicy.Decide(false, false, true, false, SkipReason.None).SelectChoice.Should().BeFalse();
    }

    [Fact]
    public void ChallengePolicy_ChoosePasskeyTitle() {
        Win1125H2ChallengePolicy.IsChooseAPasskeyTitle("Choose a passkey", ["Choose a passkey"]).Should().BeTrue();
    }

    [Theory]
    [InlineData(null, Win1125H2ChallengeAction.IgnoreMissingName)]
    [InlineData("Security key", Win1125H2ChallengeAction.AlreadySecurityKey)]
    public void ChallengePolicy_SecurityKeyWithoutPin(string? name, Win1125H2ChallengeAction expected) {
        Win1125H2ChallengePolicy.DecideChallenge(name, ["Security key"], PinMode.Off, null, false).Should().Be(expected);
    }

    [Fact]
    public void ChallengePolicy_AutosubmitsPin() {
        Win1125H2ChallengePolicy.DecideChallenge("Security key", ["Security key"], PinMode.Length, 6, false)
            .Should().Be(Win1125H2ChallengeAction.AutosubmitSecurityKeyPin);
        Win1125H2ChallengePolicy.DecideChallenge("Security key", ["Security key"], PinMode.Cache, null, false)
            .Should().Be(Win1125H2ChallengeAction.AutosubmitSecurityKeyPin);
    }

    [Fact]
    public void ChallengePolicy_InvokesOtherPasskey() {
        Win1125H2ChallengePolicy.DecideChallenge("Windows Hello", ["Security key"], PinMode.Off, null, true)
            .Should().Be(Win1125H2ChallengeAction.InvokeChooseDifferentPasskey);
    }

    [Fact]
    public void ChallengePolicy_LeavesHelloWhenAsk() {
        Win1125H2ChallengePolicy.DecideChallenge("Windows Hello", ["Security key"], PinMode.Off, null, false)
            .Should().Be(Win1125H2ChallengeAction.LeaveAlone);
    }

}
