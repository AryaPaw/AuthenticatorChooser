namespace AuthenticatorChooser.Fido;

public static class PinFillRetryPolicy {

    public static readonly IReadOnlyList<int> DelayMs = [0, 80, 160, 320, 640];

    public static readonly TimeSpan FindFieldTimeout = TimeSpan.FromSeconds(2);

}
