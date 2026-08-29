using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class MutexSingleInstanceTests {

    [Fact]
    public void SecondAcquireFails_AndWakeupSignals() {
        string sid = "testsid" + Guid.NewGuid().ToString("N");
        using MutexSingleInstanceService first = new(sid);
        first.TryAcquire().Should().BeTrue();

        using MutexSingleInstanceService second = new(sid);
        second.TryAcquire().Should().BeFalse();

        int hits = 0;
        first.WatchShowWindow(() => Interlocked.Increment(ref hits));
        second.SignalShowWindow();

        DateTime deadline = DateTime.UtcNow.AddSeconds(3);
        while (Volatile.Read(ref hits) == 0 && DateTime.UtcNow < deadline) {
            Thread.Sleep(50);
        }

        hits.Should().BeGreaterThan(0);
    }

}
