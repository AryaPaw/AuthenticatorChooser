using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class PinFillRetryPolicyTests {

    [Fact]
    public void Delays_RetryAfterTheIslandAppears() {
        PinFillRetryPolicy.DelayMs.Should().Equal(0, 80, 160, 320, 640);
        PinFillRetryPolicy.FindFieldTimeout.Should().Be(TimeSpan.FromSeconds(2));
    }

}