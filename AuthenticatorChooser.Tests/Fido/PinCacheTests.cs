using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class PinCacheTests {

    [Fact]
    public void StoreUseAndZero_RoundTripsWithoutKeepingPlaintextField() {
        using PinCache cache = Create(count: 1);
        cache.TryStore("2468").Should().Be(PinCacheStoreResult.Stored);
        cache.HasCached.Should().BeTrue();
        string? recovered = null;
        cache.TryUse(bstr => {
            recovered = MarshalString(bstr);
            return true;
        }).Should().BeTrue();
        recovered.Should().Be("2468");
        cache.Clear();
        cache.HasCached.Should().BeFalse();
        cache.TryUse(_ => true).Should().BeFalse();
    }

    [Fact]
    public void Store_RejectsWrongDeviceCountAndShortPin() {
        using PinCache none = Create(count: 0);
        none.TryStore("2468").Should().Be(PinCacheStoreResult.RejectedDeviceCount);
        using PinCache many = Create(count: 2);
        many.TryStore("2468").Should().Be(PinCacheStoreResult.RejectedDeviceCount);
        using PinCache unknown = Create(count: null);
        unknown.TryStore("2468").Should().Be(PinCacheStoreResult.RejectedDeviceCount);
        using PinCache one = Create(count: 1);
        one.TryStore("12").Should().Be(PinCacheStoreResult.RejectedLength);
    }

    [Fact]
    public void Store_RejectsDebuggerAndClears() {
        StubDebugger debugger = new() { IsAttached = true };
        using PinCache cache = Create(count: 1, debugger: debugger);
        cache.TryStore("2468").Should().Be(PinCacheStoreResult.RejectedDebugger);
        debugger.IsAttached = false;
        cache.TryStore("2468").Should().Be(PinCacheStoreResult.Stored);
        debugger.IsAttached = true;
        cache.HasCached.Should().BeFalse();
    }

    [Fact]
    public void ExpiresOnMonotonicClock() {
        StubClock clock = new() { TickCount64 = 1_000 };
        using PinCache cache = Create(count: 1, clock: clock);
        cache.Lifetime = PinCacheLifetime.OneMinute;
        cache.TryStore("2468").Should().Be(PinCacheStoreResult.Stored);
        cache.RemainingSeconds.Should().Be(60);
        clock.TickCount64 = 1_000 + 61_000;
        cache.HasCached.Should().BeFalse();
    }

    [Fact]
    public void UntilLockOrExit_DoesNotExpireOnTimer() {
        StubClock clock = new() { TickCount64 = 5 };
        using PinCache cache = Create(count: 1, clock: clock);
        cache.Lifetime = PinCacheLifetime.UntilLockOrExit;
        cache.TryStore("2468").Should().Be(PinCacheStoreResult.Stored);
        clock.TickCount64 = 5 + (long) TimeSpan.FromHours(3).TotalMilliseconds;
        cache.HasCached.Should().BeTrue();
        cache.RemainingSeconds.Should().Be(int.MaxValue);
        cache.Dispose();
        Action disposed = () => _ = cache.HasCached;
        disposed.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Use_FailClosedWhenDeviceCountChanges() {
        StubFido2DeviceCounter devices = new() { Count = 1 };
        using PinCache cache = Create(devices: devices);
        cache.TryStore("2468").Should().Be(PinCacheStoreResult.Stored);
        devices.Count = 2;
        cache.TryUse(_ => true).Should().BeFalse();
        devices.Count = 1;
        cache.HasCached.Should().BeTrue();
    }

    [Fact]
    public void CountCtap_CountsUsagePageOnly() {
        Fido2Devices.CountCtap([0xF1D0, 0x0001, 0xF1D0]).Should().Be(2);
        Fido2Devices.CountCtap([]).Should().Be(0);
    }

    private static PinCache Create(int? count = 1, IDebuggerProbe? debugger = null, IMonotonicClock? clock = null, IFido2DeviceCounter? devices = null) =>
        new(devices ?? new StubFido2DeviceCounter { Count = count }, debugger ?? new StubDebugger(), clock ?? new StubClock());

    private static string MarshalString(IntPtr bstr) => System.Runtime.InteropServices.Marshal.PtrToStringBSTR(bstr);

    private sealed class StubFido2DeviceCounter: IFido2DeviceCounter {
        public int? Count { get; set; } = 1;
        public int? CountCtapHid() => Count;
    }

    private sealed class StubClock: IMonotonicClock {
        public long TickCount64 { get; set; } = 1;
    }

    private sealed class StubDebugger: IDebuggerProbe {
        public bool IsAttached { get; set; }
    }

}
