using System.Security.Cryptography;

namespace AuthenticatorChooser.Updates;

internal static class SetupIntegrity {

    public static bool TryParseGitHubDigest(string? digest, out string hex) {
        hex = "";
        if (string.IsNullOrWhiteSpace(digest)) {
            return false;
        }

        string trimmed = digest.Trim();
        const string prefix = "sha256:";
        if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        string candidate = trimmed[prefix.Length..].Trim();
        if (candidate.Length != 64 || !candidate.All(Uri.IsHexDigit)) {
            return false;
        }

        hex = candidate.ToLowerInvariant();
        return true;
    }

    public static string HashFile(string path) {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.None);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool HashesMatch(string expectedHex, string actualHex) =>
        string.Equals(expectedHex, actualHex, StringComparison.OrdinalIgnoreCase);

}
