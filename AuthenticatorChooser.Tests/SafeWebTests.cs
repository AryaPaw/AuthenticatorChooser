using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class SafeWebTests {

    [Fact]
    public void TryCreateAllowedUrl_AcceptsGithubHttps() {
        SafeWeb.TryCreateAllowedUrl(AppCredits.OriginalRepositoryUrl, out Uri? uri).Should().BeTrue();
        uri!.Host.Should().Be("github.com");
        SafeWeb.TryCreateAllowedUrl(AppCredits.ForkRepositoryUrl, out _).Should().BeTrue();
    }

    [Fact]
    public void TryCreateAllowedUrl_RejectsNonGithubAndNonHttps() {
        SafeWeb.TryCreateAllowedUrl("http://github.com/Aldaviva/AuthenticatorChooser", out _).Should().BeFalse();
        SafeWeb.TryCreateAllowedUrl("https://example.com", out _).Should().BeFalse();
        SafeWeb.TryCreateAllowedUrl("not-a-url", out _).Should().BeFalse();
    }

}
