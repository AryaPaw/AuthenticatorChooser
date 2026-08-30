using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class PinCacheUxPolicyTests {

    [Fact]
    public void CacheMode_IgnoresLengthModeSixUntilARealPinIsTyped() {
        PinCacheUxPolicy.SubmitLength(PinMode.Cache, 6, null).Should().BeNull();
        PinCacheUxPolicy.SubmitLength(PinMode.Cache, 6, 10).Should().Be(10);
        PinCacheUxPolicy.SubmitLength(PinMode.Length, 6, 10).Should().Be(6);
        PinCacheUxPolicy.AutosubmitFirstTypedPin(null).Should().BeFalse();
        PinCacheUxPolicy.AutosubmitFirstTypedPin(10).Should().BeTrue();
    }

    [Fact]
    public void RememberedLength_UsesThePinThatWasActuallySubmitted() {
        PinCacheUxPolicy.RememberedLength(10).Should().Be(10);
        PinCacheUxPolicy.RememberedLength(6).Should().Be(6);
        PinCacheUxPolicy.RememberedLength(2).Should().BeNull();
    }

    [Fact]
    public void WaitingStatus_AsksForEnterUntilLengthIsLearned() {
        PinCacheUxPolicy.WaitingStatus(null).Should().Contain("press Enter");
        PinCacheUxPolicy.WaitingStatus(10).Should().Contain("10 characters").And.Contain("OK is pressed");
    }

}
