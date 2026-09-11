namespace AuthenticatorChooser.Tests;

internal static class StaHarness {

    public static void Run(Action action, TimeSpan? timeout = null) {
        Exception? error = null;
        Thread thread = new(() => {
            try {
                action();
            } catch (Exception e) {
                error = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        TimeSpan wait = timeout ?? TimeSpan.FromSeconds(30);
        if (!thread.Join(wait)) {
            throw new TimeoutException($"STA test did not finish within {wait.TotalSeconds:0} seconds.");
        }

        if (error is not null) {
            throw error;
        }
    }

}
