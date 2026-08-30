namespace AuthenticatorChooser.Fido;

public static class PinKeyMap {

    public static char? FromVirtualKey(uint vkCode, bool shift) {
        if (vkCode is >= 0x60 and <= 0x69) {
            return (char) ('0' + (vkCode - 0x60));
        }

        if (!shift && vkCode is >= 0x30 and <= 0x39) {
            return (char) vkCode;
        }

        if (vkCode is >= 0x41 and <= 0x5A) {
            return shift ? (char) vkCode : char.ToLowerInvariant((char) vkCode);
        }

        return null;
    }

}
