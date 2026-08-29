using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class FidoWindowTests {

    [Fact]
    public void DetectsFidoAndAltTabClasses() {
        FidoWindow.IsFidoPromptClass(FidoWindow.ClassName).Should().BeTrue();
        FidoWindow.IsFidoPromptClass("Other").Should().BeFalse();
        FidoWindow.IsAltTabHeld(FidoWindow.AltTabClassName).Should().BeTrue();
        FidoWindow.IsAltTabHeld("X").Should().BeFalse();
    }

}
