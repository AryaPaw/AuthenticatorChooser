using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class UpdatePolicyTests {

    [Fact]
    public void RejectsOffRepoUrlsAndAllowsGithubCdnRedirects() {
        UpdatePolicy.IsAllowedAssetUrl(new Uri("https://evil.example/AryaPaw/AuthenticatorChooser/x.exe")).Should().BeFalse();
        UpdatePolicy.IsAllowedAssetUrl(new Uri("https://github.com/AryaPaw/AuthenticatorChooser/releases/download/v0.8.4/AuthenticatorChooser-Setup-win-x64.exe")).Should().BeTrue();
        UpdatePolicy.IsAllowedAssetUrl(new Uri("https://github.com/AryaPaw/AuthenticatorChooser/archive/refs/heads/main.zip")).Should().BeFalse();
        UpdatePolicy.IsAllowedAssetUrl(new Uri("https://raw.githubusercontent.com/AryaPaw/AuthenticatorChooser/main/setup.exe")).Should().BeFalse();
        UpdatePolicy.IsAllowedAssetUrl(new Uri("https://objects.githubusercontent.com/github-production-release-asset-2e65be/foo")).Should().BeFalse();
        UpdatePolicy.IsAllowedRedirectUrl(new Uri("https://objects.githubusercontent.com/github-production-release-asset-2e65be/foo")).Should().BeTrue();
        UpdatePolicy.IsAllowedRedirectUrl(new Uri("https://release-assets.githubusercontent.com/github-production-release-asset/foo")).Should().BeTrue();
        UpdatePolicy.IsAllowedRedirectUrl(new Uri("https://github-releases.githubusercontent.com/github-production-release-asset/foo")).Should().BeTrue();
        UpdatePolicy.IsAllowedRedirectUrl(new Uri("https://raw.githubusercontent.com/AryaPaw/AuthenticatorChooser/main/setup.exe")).Should().BeFalse();
        UpdatePolicy.IsAllowedRedirectUrl(new Uri("https://githubusercontent.com.evil.example/x")).Should().BeFalse();
        UpdatePolicy.IsAllowedRedirectUrl(new Uri("http://release-assets.githubusercontent.com/foo")).Should().BeFalse();
    }

    [Fact]
    public void Normalize_StripsPrefixAndBuildMetadata() {
        UpdatePolicy.Normalize("v0.8.4+abc").Should().Be("0.8.4");
        UpdatePolicy.Normalize("0.8.4").Should().Be("0.8.4");
    }

    [Fact]
    public void RejectsSiblingPathOutsideUpdatesRoot() {
        string root = Path.Combine(Path.GetTempPath(), "ac-updates-root");
        UpdatePolicy.IsInsideRoot(root, Path.Combine(root, "setup.exe")).Should().BeTrue();
        UpdatePolicy.IsInsideRoot(root, Path.Combine(root + "-evil", "setup.exe")).Should().BeFalse();
    }

}
