namespace AuthenticatorChooser.Fido;

public enum PinCacheLifetime {
    OneMinute,
    TwoMinutes,
    FiveMinutes,
    TenMinutes,
    UntilLockOrExit
}

public static class PinCacheLifetimePolicy {

    public static int TtlSeconds(PinCacheLifetime lifetime) => lifetime switch {
        PinCacheLifetime.OneMinute => 60,
        PinCacheLifetime.TwoMinutes => 120,
        PinCacheLifetime.FiveMinutes => 300,
        PinCacheLifetime.TenMinutes => 600,
        PinCacheLifetime.UntilLockOrExit => 0,
        _ => throw new InvalidOperationException($"Unhandled PIN cache lifetime {lifetime}")
    };

    public static string Label(PinCacheLifetime lifetime) => lifetime switch {
        PinCacheLifetime.OneMinute => "1 minute",
        PinCacheLifetime.TwoMinutes => "2 minutes",
        PinCacheLifetime.FiveMinutes => "5 minutes",
        PinCacheLifetime.TenMinutes => "10 minutes",
        PinCacheLifetime.UntilLockOrExit => "Until lock or exit",
        _ => throw new InvalidOperationException($"Unhandled PIN cache lifetime {lifetime}")
    };

    public static IReadOnlyList<PinCacheLifetime> All { get; } = [
        PinCacheLifetime.OneMinute,
        PinCacheLifetime.TwoMinutes,
        PinCacheLifetime.FiveMinutes,
        PinCacheLifetime.TenMinutes,
        PinCacheLifetime.UntilLockOrExit
    ];

}
