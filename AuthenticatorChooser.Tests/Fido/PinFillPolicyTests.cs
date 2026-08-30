using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class PinFillPolicyTests {

    [Fact]
    public void Off_DoesNothing() {
        PinFillPolicy.Decide(PinMode.Off, true, true, 1, false, false).Should().Be(PinFillDecision.DoNothing);
        PinFillPolicy.WantsPinDialog(PinMode.Off, 6).Should().BeFalse();
    }

    [Fact]
    public void Length_WatchesTypedCount() {
        PinFillPolicy.Decide(PinMode.Length, false, false, 0, false, false).Should().Be(PinFillDecision.WatchLength);
        PinFillPolicy.WantsPinDialog(PinMode.Length, 6).Should().BeTrue();
        PinFillPolicy.WantsPinDialog(PinMode.Length, null).Should().BeFalse();
    }

    [Fact]
    public void Cache_FillsWhenTrustedSingleKey() {
        PinFillPolicy.Decide(PinMode.Cache, true, true, 1, false, false).Should().Be(PinFillDecision.FillCache);
        PinFillPolicy.WantsPinDialog(PinMode.Cache, null).Should().BeTrue();
    }

    [Fact]
    public void Cache_FailClosedWithoutUnsafeFallback() {
        PinFillPolicy.Decide(PinMode.Cache, true, false, 1, false, false).Should().Be(PinFillDecision.ManualFallback);
        PinFillPolicy.Decide(PinMode.Cache, true, true, 0, false, false).Should().Be(PinFillDecision.ManualFallback);
        PinFillPolicy.Decide(PinMode.Cache, true, true, 2, false, false).Should().Be(PinFillDecision.ManualFallback);
        PinFillPolicy.Decide(PinMode.Cache, true, true, null, false, false).Should().Be(PinFillDecision.ManualFallback);
        PinFillPolicy.Decide(PinMode.Cache, false, true, 1, false, false).Should().Be(PinFillDecision.LearnFromPrompt);
        PinFillPolicy.Decide(PinMode.Cache, false, false, 1, false, false).Should().Be(PinFillDecision.ManualFallback);
        PinFillPolicy.Decide(PinMode.Cache, true, false, 1, false, false).Should().Be(PinFillDecision.ManualFallback);
    }

    [Fact]
    public void Cache_RepeatPromptOrDebuggerClears() {
        PinFillPolicy.Decide(PinMode.Cache, true, true, 1, true, false).Should().Be(PinFillDecision.RefuseAndClear);
        PinFillPolicy.Decide(PinMode.Cache, true, true, 1, false, true).Should().Be(PinFillDecision.RefuseAndClear);
    }

}
