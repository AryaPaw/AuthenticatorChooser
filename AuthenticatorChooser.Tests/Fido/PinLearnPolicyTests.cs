using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class PinLearnPolicyTests {

    [Fact]
    public void Capture_AllowsSameWindowOrSameProcess() {
        PinLearnPolicy.IsCaptureForeground(10, 10, 5, 5).Should().BeTrue();
        PinLearnPolicy.IsCaptureForeground(11, 10, 5, 5).Should().BeTrue();
        PinLearnPolicy.IsCaptureForeground(11, 10, 5, 6).Should().BeFalse();
        PinLearnPolicy.IsCaptureForeground(IntPtr.Zero, 10, 5, 5).Should().BeFalse();
        PinLearnPolicy.IsCaptureForeground(10, 10, 0, 5).Should().BeFalse();
        PinLearnPolicy.IsCaptureForeground(11, 10, 5, 6, 10, 10).Should().BeTrue();
        PinLearnPolicy.IsCaptureForeground(11, 10, 5, 6, 12, 10).Should().BeFalse();
        PinLearnPolicy.IsCaptureForeground(11, 10, 5, 6, 12, 10, 20, 20).Should().BeTrue();
        PinLearnPolicy.IsCaptureForeground(11, 10, 5, 6, 12, 10, 20, 21).Should().BeFalse();
    }

    [Fact]
    public void ResolveDialogHwnd_PrefersAutomationHandle() {
        PinLearnPolicy.ResolveDialogHwnd(3, 9).Should().Be((IntPtr) 3);
        PinLearnPolicy.ResolveDialogHwnd(IntPtr.Zero, 9).Should().Be((IntPtr) 9);
    }

    [Fact]
    public void LookForPinOnTitle_SkipsHelloWhenCaching() {
        PinLearnPolicy.LookForPinOnTitle(PinMode.Cache, null, false).Should().BeTrue();
        PinLearnPolicy.LookForPinOnTitle(PinMode.Cache, null, true).Should().BeFalse();
        PinLearnPolicy.LookForPinOnTitle(PinMode.Length, 6, false).Should().BeTrue();
        PinLearnPolicy.LookForPinOnTitle(PinMode.Length, null, false).Should().BeFalse();
        PinLearnPolicy.LookForPinOnTitle(PinMode.Off, null, false).Should().BeFalse();
    }

}
