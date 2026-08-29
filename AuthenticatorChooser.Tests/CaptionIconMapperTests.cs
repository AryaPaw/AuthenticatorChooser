using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class CaptionIconMapperTests {

    [Theory]
    [InlineData(CaptionIconMapper.WindowLogo, PromptFamily.Win1123H2)]
    [InlineData(CaptionIconMapper.WindowSecurityLogo, PromptFamily.Win1125H2)]
    [InlineData("Unknown", null)]
    [InlineData(null, null)]
    public void FromAutomationId_MapsKnownIcons(string? id, PromptFamily? expected) {
        CaptionIconMapper.FromAutomationId(id).Should().Be(expected);
    }

}
