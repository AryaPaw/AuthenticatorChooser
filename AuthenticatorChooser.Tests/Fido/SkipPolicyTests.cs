using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class SkipPolicyTests {

    [Theory]
    [InlineData(false, false, false, true, SkipReason.Paused)]
    [InlineData(true, true, false, true, SkipReason.ShiftHeld)]
    [InlineData(true, false, false, false, SkipReason.ExtraOptions)]
    [InlineData(true, false, true, false, SkipReason.None)]
    [InlineData(true, false, false, true, SkipReason.None)]
    public void Decide_ReturnsExpectedReason(bool enabled, bool shift, bool skipAll, bool onlyKeyAndPhone, SkipReason expected) {
        SkipPolicy.Decide(enabled, shift, skipAll, onlyKeyAndPhone).Should().Be(expected);
    }

    [Theory]
    [InlineData(SkipReason.None, ChooserEventKind.ChoseSecurityKey)]
    [InlineData(SkipReason.Paused, ChooserEventKind.Paused)]
    [InlineData(SkipReason.ShiftHeld, ChooserEventKind.ShiftHeld)]
    [InlineData(SkipReason.ExtraOptions, ChooserEventKind.ExtraOptions)]
    [InlineData(SkipReason.PinModeOff, ChooserEventKind.PinModeOff)]
    public void ToEvent_MapsReasons(SkipReason reason, ChooserEventKind expected) {
        SkipPolicy.ToEvent(reason).Should().Be(expected);
    }

}
