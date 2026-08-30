using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class PinFillHwndPolicyTests {

    [Fact]
    public void SearchOrder_SkipsZeroAndDuplicates() {
        PinFillHwndPolicy.SearchOrder(IntPtr.Zero, IntPtr.Zero).Should().BeEmpty();
        PinFillHwndPolicy.SearchOrder(8, IntPtr.Zero).Should().Equal((IntPtr) 8);
        PinFillHwndPolicy.SearchOrder(8, 3).Should().Equal((IntPtr) 3, (IntPtr) 8);
        PinFillHwndPolicy.SearchOrder(5, 5).Should().Equal((IntPtr) 5);
        PinFillHwndPolicy.SearchOrder(8, 3, 9, IntPtr.Zero, 3).Should().Equal((IntPtr) 3, (IntPtr) 9, (IntPtr) 8);
    }

}
