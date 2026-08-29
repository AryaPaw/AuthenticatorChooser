using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class PinModePolicyTests {

    [Fact]
    public void View_OffShowsTurnOn() {
        PinModeView off = PinModePolicy.View(null);
        off.ButtonText.Should().Be("Turn on");
        off.FieldEnabled.Should().BeTrue();
        off.Summary.Should().Contain("Off");
    }

    [Fact]
    public void View_OnShowsTurnOff() {
        PinModeView on = PinModePolicy.View(6);
        on.ButtonText.Should().Be("Turn off");
        on.FieldEnabled.Should().BeFalse();
        on.Summary.Should().Contain("6");
    }

    [Fact]
    public void Press_TurnOnSavesLengthWithoutKeepingSecret() {
        PinToggleDecision on = PinModePolicy.Press(null, "123456");
        on.Kind.Should().Be(PinToggleKind.TurnOn);
        on.LengthAfter.Should().Be(6);
        on.View.ButtonText.Should().Be("Turn off");
    }

    [Fact]
    public void Press_TurnOffClearsLength() {
        PinToggleDecision off = PinModePolicy.Press(6, "ignored");
        off.Kind.Should().Be(PinToggleKind.TurnOff);
        off.LengthAfter.Should().BeNull();
        off.View.ButtonText.Should().Be("Turn on");
    }

    [Fact]
    public void Press_EmptyOrShortDoesNotArm() {
        PinModePolicy.Press(null, null).Kind.Should().Be(PinToggleKind.RejectedEmpty);
        PinModePolicy.Press(null, "12").Kind.Should().Be(PinToggleKind.RejectedNeedLength);
        PinModePolicy.Press(null, "12").LengthAfter.Should().BeNull();
    }

}
