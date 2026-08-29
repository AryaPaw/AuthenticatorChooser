using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class AppCreditsTests {

    [Fact]
    public void Attribution_NamesOriginalAuthorAndFork() {
        AppCredits.Attribution.Should().Contain("Ben Hutchison");
        AppCredits.Attribution.Should().Contain("AryaPaw");
        AppCredits.CopyrightLine.Should().Contain("©");
        AppCredits.OriginalRepositoryUrl.Should().Contain("Aldaviva");
        AppCredits.ForkRepositoryUrl.Should().Contain("AryaPaw");
    }

}
