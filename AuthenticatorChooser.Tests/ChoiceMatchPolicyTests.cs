using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class ChoiceMatchPolicyTests {

    [Fact]
    public void NameContainsAny_MatchesSubstring() {
        ChoiceMatchPolicy.NameContainsAny("USB Security key option", ["Security key"]).Should().BeTrue();
        ChoiceMatchPolicy.NameContainsAny("Phone", ["Security key"]).Should().BeFalse();
    }

    [Fact]
    public void IsOnlySecurityKeyAndNewPhone_AllowsPhonePlusDesired() {
        object key = new();
        object phone = new();
        IReadOnlyList<ChoiceMatch> choices = [
            new ChoiceMatch(key, "Security key"),
            new ChoiceMatch(phone, "iPhone, iPad, or Android device")
        ];
        ChoiceMatchPolicy.IsOnlySecurityKeyAndNewPhone(choices, new ChoiceMatch(key, "Security key"), ["iPhone, iPad, or Android device"]).Should().BeTrue();
    }

    [Fact]
    public void IsOnlySecurityKeyAndNewPhone_RejectsWindowsHello() {
        object key = new();
        object hello = new();
        IReadOnlyList<ChoiceMatch> choices = [
            new ChoiceMatch(key, "Security key"),
            new ChoiceMatch(hello, "This Windows device")
        ];
        ChoiceMatchPolicy.IsOnlySecurityKeyAndNewPhone(choices, new ChoiceMatch(key, "Security key"), ["iPhone"]).Should().BeFalse();
    }

    [Fact]
    public void FindByNameSubstring_ReturnsFirstMatch() {
        ChoiceMatch[] choices = [new(new object(), "Windows Hello"), new(new object(), "Security key")];
        ChoiceMatchPolicy.FindByNameSubstring(choices, ["Security key"])!.Value.Name.Should().Be("Security key");
        ChoiceMatchPolicy.FindByNameSubstring(choices, ["missing"]).Should().BeNull();
    }

}
