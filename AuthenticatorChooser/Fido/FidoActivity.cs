namespace AuthenticatorChooser.Fido;

internal static class FidoActivity {

    private static int active;

    public static bool IsInProgress => Volatile.Read(ref active) > 0;

    public static IDisposable Begin() {
        Interlocked.Increment(ref active);
        return new Scope();
    }

    private sealed class Scope: IDisposable {

        private int disposed;

        public void Dispose() {
            if (Interlocked.Exchange(ref disposed, 1) == 0) {
                Interlocked.Decrement(ref active);
            }
        }

    }

}
