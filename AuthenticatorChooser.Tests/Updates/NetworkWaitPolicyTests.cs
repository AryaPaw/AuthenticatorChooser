using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class NetworkWaitPolicyTests {

    [Fact]
    public async Task WaitUntilOnline_ReturnsTrueWhenProbeSucceeds() {
        SequentialProbe probe = new(false, false, true);
        bool online = await NetworkWaitPolicy.WaitUntilOnline(
            probe,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(1),
            CancellationToken.None);
        online.Should().BeTrue();
        probe.Calls.Should().Be(3);
    }

    [Fact]
    public async Task WaitUntilOnline_ReturnsFalseWhenBudgetExpires() {
        SequentialProbe probe = new(false, false, false);
        bool online = await NetworkWaitPolicy.WaitUntilOnline(
            probe,
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(10),
            CancellationToken.None);
        online.Should().BeFalse();
        probe.Calls.Should().BeGreaterThan(0);
    }

    [Fact]
    public void NextOfflineRetry_IsShort() {
        NetworkWaitPolicy.OfflineRetry.Should().Be(TimeSpan.FromSeconds(15));
    }

    private sealed class SequentialProbe: IInternetProbe {

        private readonly Queue<bool> answers;

        public SequentialProbe(params bool[] answers) {
            this.answers = new Queue<bool>(answers);
        }

        public int Calls { get; private set; }

        public Task<bool> IsReachable(CancellationToken cancellationToken) {
            Calls++;
            bool value = answers.Count > 0 && answers.Dequeue();
            if (answers.Count == 0 && !value) {
                answers.Enqueue(false);
            }

            return Task.FromResult(value);
        }

    }

}
