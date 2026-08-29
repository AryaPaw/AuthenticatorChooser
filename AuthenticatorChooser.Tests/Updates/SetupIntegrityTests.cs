using System.Security.Cryptography;
using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class SetupIntegrityTests {

    [Fact]
    public void TryParseSidecar_AcceptsGnuAndBareHex() {
        const string name = "AuthenticatorChooser-Setup-win-x64.exe";
        byte[] payload = [1];
        string hex = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        SetupIntegrity.TryParseSidecar(hex, name, out string bare).Should().BeTrue();
        bare.Should().Be(hex);
        SetupIntegrity.TryParseSidecar(hex + "  " + name, name, out string gnu).Should().BeTrue();
        gnu.Should().Be(hex);
    }

    [Fact]
    public void TryParseSidecar_RejectsWrongNameAndShortHex() {
        SetupIntegrity.TryParseSidecar("abcd  other.exe", "AuthenticatorChooser-Setup-win-x64.exe", out _).Should().BeFalse();
        SetupIntegrity.TryParseSidecar("not-hex", "AuthenticatorChooser-Setup-win-x64.exe", out _).Should().BeFalse();
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
