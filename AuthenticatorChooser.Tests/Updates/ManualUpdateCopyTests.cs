using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class ManualUpdateCopyTests {

    [Fact]
    public void For_CoversEveryOutcome() {
        foreach (SilentUpdateOutcome outcome in Enum.GetValues<SilentUpdateOutcome>()) {
            ManualUpdateCopy.For(outcome).Should().NotBeNullOrWhiteSpace();
        }
    }

}
