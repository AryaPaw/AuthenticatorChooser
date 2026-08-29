namespace AuthenticatorChooser.Updates;

public interface IInternetProbe {

    Task<bool> IsReachable(CancellationToken cancellationToken);

}

public static class NetworkWaitPolicy {

    public static readonly TimeSpan OfflineRetry = TimeSpan.FromSeconds(15);

    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    public static async Task<bool> WaitUntilOnline(
        IInternetProbe probe,
        TimeSpan budget,
        TimeSpan retry,
        CancellationToken cancellationToken) {
        System.Diagnostics.Stopwatch elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (true) {
            if (await probe.IsReachable(cancellationToken)) {
                return true;
            }

            if (budget == Timeout.InfiniteTimeSpan) {
                await Task.Delay(retry, cancellationToken);
                continue;
            }

            if (elapsed.Elapsed >= budget) {
                return false;
            }

            TimeSpan remaining = budget - elapsed.Elapsed;
            TimeSpan delay = remaining < retry ? remaining : retry;
            if (delay <= TimeSpan.Zero) {
                return false;
            }

            await Task.Delay(delay, cancellationToken);
        }
    }

}
