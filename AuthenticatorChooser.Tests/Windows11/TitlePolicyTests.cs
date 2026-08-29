using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class TitlePolicyTests {

    [Fact]
    public void EqualsAny_RejectsNull() {
        TitlePolicy.EqualsAny(null, ["Choose a passkey"]).Should().BeFalse();
    }

    [Fact]
    public void CanHandleWin1123H2_IncludesMakingSureWhenAggressive() {
        TitlePolicy.CanHandleWin1123H2("Making sure it's you", true, ["Sign in with your passkey"], ["Making sure it's you"]).Should().BeTrue();
        TitlePolicy.CanHandleWin1123H2("Making sure it's you", false, ["Sign in with your passkey"], ["Making sure it's you"]).Should().BeFalse();
    }

    [Fact]
    public void CanHandleWin1125H2_IncludesSignInWhenAggressive() {
        TitlePolicy.CanHandleWin1125H2("Sign in with a passkey", true, ["Choose a passkey"], ["Sign in with a passkey"]).Should().BeTrue();
        TitlePolicy.CanHandleWin1125H2("Sign in with a passkey", false, ["Choose a passkey"], ["Sign in with a passkey"]).Should().BeFalse();
        TitlePolicy.CanHandleWin1125H2("Choose a passkey", false, ["Choose a passkey"], ["Sign in with a passkey"]).Should().BeTrue();
    }

    [Fact]
    public void IncludeAggressiveTitles_WhenSkipAllOrPin() {
        TitlePolicy.IncludeAggressiveTitles(true, null).Should().BeTrue();
        TitlePolicy.IncludeAggressiveTitles(false, 6).Should().BeTrue();
        TitlePolicy.IncludeAggressiveTitles(false, null).Should().BeFalse();
    }

}
