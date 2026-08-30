using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class PinKeyMapTests {

    [Fact]
    public void FromVirtualKey_MapsDigitsLettersWithoutToUnicode() {
        PinKeyMap.FromVirtualKey(0x31, false).Should().Be('1');
        PinKeyMap.FromVirtualKey(0x31, true).Should().BeNull();
        PinKeyMap.FromVirtualKey(0x61, false).Should().Be('1');
        PinKeyMap.FromVirtualKey(0x41, false).Should().Be('a');
        PinKeyMap.FromVirtualKey(0x41, true).Should().Be('A');
        PinKeyMap.FromVirtualKey(0x0D, false).Should().BeNull();
    }

}