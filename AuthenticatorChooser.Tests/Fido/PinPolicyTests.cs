using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class PinPolicyTests {

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(3)]
    public void Normalize_RejectsShortPins(int? raw) {
        PinPolicy.Normalize(raw).Should().BeNull();
        PinPolicy.ShouldAutosubmit(PinPolicy.Normalize(raw)).Should().BeFalse();
    }

    [Fact]
    public void CaptureFromTypedSecret_SavesLengthAndRejectsShort() {
        PinPolicy.CaptureFromTypedSecret("123456").Should().Be(new PinCaptureOutcome(PinCaptureKind.Saved, 6));
        PinPolicy.CaptureFromTypedSecret("12").Kind.Should().Be(PinCaptureKind.Rejected);
        PinPolicy.CaptureFromTypedSecret("").Kind.Should().Be(PinCaptureKind.Unchanged);
        PinPolicy.CaptureFromTypedSecret(null).Kind.Should().Be(PinCaptureKind.Unchanged);
        PinPolicy.Normalize(64).Should().BeNull();
        PinPolicy.SavedLengthSummary(6).Should().Contain("6");
        PinPolicy.LiveCountLabel(0).Should().Contain("0");
    }

    [Fact]
    public void LengthOfUiaValue_TreatsNullAsZero() {
        PinAutosubmit.LengthOfUiaValue(null).Should().Be(0);
        PinAutosubmit.LengthOfUiaValue("abcd").Should().Be(4);
        PinAutosubmit.LengthOfUiaValue(12).Should().Be(2);
    }

}
