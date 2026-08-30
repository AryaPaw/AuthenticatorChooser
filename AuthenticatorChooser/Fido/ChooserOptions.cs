using System.Diagnostics;
using AuthenticatorChooser.Windows11;

namespace AuthenticatorChooser.Fido;

public sealed class ChooserOptions {

    public ChooserOptions(AppState state) : this(state, null, null, null, null, null, null, null) { }

    internal ChooserOptions(
        AppState state,
        IPinCache? pinCache,
        IWindowTrust? windowTrust,
        INativePinFiller? pinFiller,
        IFido2DeviceCounter? devices,
        IDebuggerProbe? debugger,
        Action? persist,
        IPinKeyHook? pinKeyHook = null) {
        this.state = state;
        this.pinCache = pinCache;
        this.windowTrust = windowTrust ?? WindowTrust.Shared;
        this.pinFiller = pinFiller ?? NativeUia.Shared;
        this.devices = devices ?? new Fido2Devices();
        this.debugger = debugger ?? new NativeDebuggerProbe();
        this.persist = persist;
        this.pinKeyHook = pinKeyHook ?? new NullPinKeyHook();
    }

    public AppState state { get; }

    internal IPinCache? pinCache { get; }

    internal IWindowTrust windowTrust { get; }

    internal INativePinFiller pinFiller { get; }

    internal IFido2DeviceCounter devices { get; }

    internal IDebuggerProbe debugger { get; }

    internal Action? persist { get; }

    internal IPinKeyHook pinKeyHook { get; }

    public Stopwatch overallStopwatch { get; } = new();

    public bool skipAllNonSecurityKeyOptions => state.SkipAllNonSecurityKeyOptions;

    public int? autoSubmitPinLength => state.AutoSubmitPinLength;

    public int? learnedPinLength => state.LearnedPinLength;

    public PinMode pinMode => state.PinMode;

    public IReadOnlyList<AuthenticatorPriorityRule> priorityRules => state.PriorityRules;

    public bool enabled => state.Enabled;

    public bool wantsAggressiveTitles =>
        TitlePolicy.IncludeAggressiveTitles(
            skipAllNonSecurityKeyOptions,
            pinMode,
            autoSubmitPinLength,
            AuthenticatorPriorityCatalog.ActionFor(priorityRules, AuthenticatorPriorityCatalog.WindowsHelloId) == AuthenticatorRuleAction.Ignore);

    public LocaleAuthenticatorNames LocaleNames() => new(
        I18N.getStrings(I18N.Key.SECURITY_KEY).ToList(),
        I18N.getStrings(I18N.Key.SMARTPHONE).ToList(),
        I18N.getStrings(I18N.Key.WINDOWS).ToList());

}
