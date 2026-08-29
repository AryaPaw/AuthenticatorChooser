using FluentAssertions;
using System.Drawing;

namespace AuthenticatorChooser.Tests;

public sealed class AppIconsTests {

    [Fact]
    public void KeyResource_IsEmbedded() {
        typeof(AppIcons).Assembly.GetManifestResourceNames().Should().Contain(AppIcons.KeyResourceName);
    }

    [Fact]
    public void CreateKeyIcon_ReturnsNonNullIcon() {
        using Icon icon = AppIcons.CreateKeyIcon();
        icon.Should().NotBeNull();
        icon.Width.Should().BePositive();
    }

}
