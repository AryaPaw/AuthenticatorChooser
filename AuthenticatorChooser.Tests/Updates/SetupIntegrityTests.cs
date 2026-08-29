using System.Security.Cryptography;
using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class SetupIntegrityTests {

    [Fact]
    public void TryParseGitHubDigest_AcceptsSha256Prefix() {
        byte[] payload = [1];
        string hex = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        SetupIntegrity.TryParseGitHubDigest("sha256:" + hex, out string parsed).Should().BeTrue();
        parsed.Should().Be(hex);
        SetupIntegrity.TryParseGitHubDigest("SHA256:" + hex.ToUpperInvariant(), out string mixed).Should().BeTrue();
        mixed.Should().Be(hex);
    }

    [Fact]
    public void TryParseGitHubDigest_RejectsMissingPrefixAndShortHex() {
        SetupIntegrity.TryParseGitHubDigest(new string('a', 64), out _).Should().BeFalse();
        SetupIntegrity.TryParseGitHubDigest("sha256:abcd", out _).Should().BeFalse();
        SetupIntegrity.TryParseGitHubDigest(null, out _).Should().BeFalse();
        SetupIntegrity.TryParseGitHubDigest("md5:" + new string('a', 32), out _).Should().BeFalse();
    }

    [Fact]
    public void HashFile_MatchesKnownPayload() {
        string path = Path.Combine(Path.GetTempPath(), "AuthenticatorChooserHash", Guid.NewGuid().ToString("N") + ".bin");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try {
            File.WriteAllBytes(path, [1]);
            string hex = SetupIntegrity.HashFile(path);
            hex.Should().Be(Convert.ToHexString(SHA256.HashData([1])).ToLowerInvariant());
            SetupIntegrity.HashesMatch(hex, hex).Should().BeTrue();
            SetupIntegrity.HashesMatch(hex, "00" + hex[2..]).Should().BeFalse();
        } finally {
            if (File.Exists(path)) {
                File.Delete(path);
            }
        }
    }

}
