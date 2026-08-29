using System.Diagnostics;

namespace AuthenticatorChooser.Fido;

public sealed class ChooserOptions(AppState state) {

    public AppState state { get; } = state;

    public Stopwatch overallStopwatch { get; } = new();

    public bool skipAllNonSecurityKeyOptions => state.SkipAllNonSecurityKeyOptions;

    public int? autoSubmitPinLength => state.AutoSubmitPinLength;

    public bool enabled => state.Enabled;

}
