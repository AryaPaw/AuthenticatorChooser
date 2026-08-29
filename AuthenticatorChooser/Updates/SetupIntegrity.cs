using System.Security.Cryptography;

namespace AuthenticatorChooser.Updates;

internal static class SetupIntegrity {

    public static string SidecarFileName(string setupFileName) => setupFileName + ".sha256";

    public static bool TryParseSidecar(string sidecarText, string expectedFileName, out string hex) {
        hex = "";
        if (string.IsNullOrWhiteSpace(sidecarText) || string.IsNullOrWhiteSpace(expectedFileName)) {
            return false;
        }

        string line = sidecarText.Replace("\r", "").Split('\n')[0].Trim();
        if (line.Length == 0) {
            return false;
        }

        string[] parts = line.Split((char[]) [' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        string candidate = parts[0].Trim();
        if (candidate.Length != 64 || !candidate.All(Uri.IsHexDigit)) {
            return false;
        }

        if (parts.Length > 1) {
            string named = Path.GetFileName(parts[1].Trim().TrimStart('*'));
            if (!string.Equals(named, expectedFileName, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
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
