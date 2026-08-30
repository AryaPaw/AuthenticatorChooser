namespace AuthenticatorChooser.Fido;

internal interface IMonotonicClock {
    long TickCount64 { get; }
}

internal sealed class SystemMonotonicClock: IMonotonicClock {
    public long TickCount64 => Environment.TickCount64;
}

internal interface IDebuggerProbe {
    bool IsAttached { get; }
}

internal sealed class NativeDebuggerProbe: IDebuggerProbe {
    public bool IsAttached => NativeSecurity.DebuggerAttached();
}

internal interface IFido2DeviceCounter {
    int? CountCtapHid();
}

public interface IPinCache: IDisposable {
    bool HasCached { get; }
    int? RemainingSeconds { get; }
    PinCacheLifetime Lifetime { get; set; }
    PinCacheStoreResult TryStore(string pin);
    bool TryUse(Func<IntPtr, bool> use);
    void Clear();
}

public enum PinCacheStoreResult {
    Stored,
    RejectedDebugger,
    RejectedDeviceCount,
    RejectedLength,
    RejectedEncrypt
}
