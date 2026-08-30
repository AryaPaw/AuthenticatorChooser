using System.Runtime.InteropServices;
using System.Text;
using NLog;

namespace AuthenticatorChooser.Fido;

internal sealed class PinCache: IPinCache {

    private const uint CryptProtectMemorySameProcess = 0x00;
    private const uint BlockSize = 16;

    private static readonly Logger Logger = LogManager.GetLogger(typeof(PinCache).FullName!);

    private readonly IFido2DeviceCounter devices;
    private readonly IDebuggerProbe debugger;
    private readonly IMonotonicClock clock;
    private readonly object gate = new();
    private byte[]? encryptedPin;
    private long cachedAtMs;
    private PinCacheLifetime lifetime = PinCacheLifetime.TwoMinutes;
    private bool disposed;

    public PinCache(IFido2DeviceCounter? devices = null, IDebuggerProbe? debugger = null, IMonotonicClock? clock = null) {
        this.devices = devices ?? new Fido2Devices();
        this.debugger = debugger ?? new NativeDebuggerProbe();
        this.clock = clock ?? new SystemMonotonicClock();
    }

    public PinCacheLifetime Lifetime {
        get {
            lock (gate) {
                return lifetime;
            }
        }
        set {
            lock (gate) {
                lifetime = value;
            }
        }
    }

    public bool HasCached {
        get {
            lock (gate) {
                return HasCachedLocked();
            }
        }
    }

    public int? RemainingSeconds {
        get {
            lock (gate) {
                if (!HasCachedLocked()) {
                    return null;
                }

                int ttl = PinCacheLifetimePolicy.TtlSeconds(lifetime);
                if (ttl <= 0) {
                    return int.MaxValue;
                }

                return Math.Max(0, ttl - (int) Math.Ceiling((clock.TickCount64 - cachedAtMs) / 1000.0));
            }
        }
    }

    public PinCacheStoreResult TryStore(string pin) {
        ArgumentNullException.ThrowIfNull(pin);
        if (debugger.IsAttached) {
            Logger.Warn("Refusing to cache the security key PIN because a debugger is attached");
            Clear();
            return PinCacheStoreResult.RejectedDebugger;
        }

        if (!PinPolicy.ShouldAutosubmit(pin.Length)) {
            return PinCacheStoreResult.RejectedLength;
        }

        if (devices.CountCtapHid() != 1) {
            Logger.Warn("Refusing to cache the security key PIN unless exactly one CTAP HID device is present");
            return PinCacheStoreResult.RejectedDeviceCount;
        }

        byte[]? buffer = null;
        try {
            byte[] plain = Encoding.UTF8.GetBytes(pin);
            uint size = (uint) ((plain.Length + BlockSize - 1) / BlockSize * BlockSize);
            buffer = new byte[size];
            plain.CopyTo(buffer, 0);
            Array.Clear(plain);

            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try {
                if (!CryptProtectMemory(handle.AddrOfPinnedObject(), size, CryptProtectMemorySameProcess)) {
                    Array.Clear(buffer);
                    return PinCacheStoreResult.RejectedEncrypt;
                }
            } finally {
                handle.Free();
            }

            lock (gate) {
                ThrowIfDisposed();
                ZeroLocked();
                encryptedPin = buffer;
                buffer = null;
                cachedAtMs = clock.TickCount64;
            }

            return PinCacheStoreResult.Stored;
        } catch (Exception exception) when (exception is not OutOfMemoryException and not ObjectDisposedException) {
            Logger.Error(exception, "Failed to cache the security key PIN");
            return PinCacheStoreResult.RejectedEncrypt;
        } finally {
            if (buffer is not null) {
                Array.Clear(buffer);
            }
        }
    }

    public bool TryUse(Func<IntPtr, bool> use) {
        ArgumentNullException.ThrowIfNull(use);
        byte[]? plain = null;
        lock (gate) {
            if (debugger.IsAttached) {
                Logger.Warn("A debugger is attached; forgetting the cached security key PIN instead of decrypting it");
                ZeroLocked();
                return false;
            }

            if (!HasCachedLocked()) {
                return false;
            }

            if (devices.CountCtapHid() != 1) {
                Logger.Warn("Refusing to fill the security key PIN unless exactly one CTAP HID device is present");
                return false;
            }

            plain = DecryptLocked();
        }

        if (plain is null) {
            return false;
        }

        try {
            int length = Array.IndexOf(plain, (byte) 0);
            if (length < 0) {
                length = plain.Length;
            }

            char[] chars = Encoding.UTF8.GetChars(plain, 0, length);
            try {
                GCHandle pinnedChars = GCHandle.Alloc(chars, GCHandleType.Pinned);
                IntPtr bstr;
                try {
                    bstr = NativeSecurity.SysAllocString(pinnedChars.AddrOfPinnedObject());
                } finally {
                    pinnedChars.Free();
                }

                if (bstr == IntPtr.Zero) {
                    return false;
                }

                try {
                    return use(bstr);
                } finally {
                    int charCount = Marshal.ReadInt32(bstr, -4);
                    NativeSecurity.RtlZeroMemory(bstr - 4, (charCount + 1) * 2 + 4);
                    NativeSecurity.SysFreeString(bstr);
                }
            } finally {
                Array.Clear(chars);
            }
        } finally {
            Array.Clear(plain);
        }
    }

    public void Clear() {
        lock (gate) {
            ZeroLocked();
        }
    }

    public void Dispose() {
        lock (gate) {
            ZeroLocked();
            disposed = true;
        }
    }

    private bool HasCachedLocked() {
        ThrowIfDisposed();
        if (encryptedPin is null) {
            return false;
        }

        if (debugger.IsAttached) {
            Logger.Warn("A debugger is attached; forgetting the cached security key PIN");
            ZeroLocked();
            return false;
        }

        if (IsExpiredLocked()) {
            ZeroLocked();
            return false;
        }

        return true;
    }

    private bool IsExpiredLocked() {
        int ttl = PinCacheLifetimePolicy.TtlSeconds(lifetime);
        return ttl > 0 && clock.TickCount64 - cachedAtMs > ttl * 1000L;
    }

    private byte[]? DecryptLocked() {
        if (encryptedPin is null) {
            return null;
        }

        byte[] buffer = (byte[]) encryptedPin.Clone();
        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try {
            if (!CryptUnprotectMemory(handle.AddrOfPinnedObject(), (uint) buffer.Length, CryptProtectMemorySameProcess)) {
                Array.Clear(buffer);
                return null;
            }

            return buffer;
        } finally {
            handle.Free();
        }
    }

    private void ZeroLocked() {
        if (encryptedPin is not null) {
            Array.Clear(encryptedPin);
            encryptedPin = null;
        }
    }

    private void ThrowIfDisposed() {
        if (disposed) {
            throw new ObjectDisposedException(nameof(PinCache));
        }
    }

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectMemory(IntPtr pDataIn, uint cbDataIn, uint dwFlags);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectMemory(IntPtr pDataIn, uint cbDataIn, uint dwFlags);

}
