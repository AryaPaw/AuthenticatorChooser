using NLog;
using System.Windows.Automation;
using Unfucked;

namespace AuthenticatorChooser.Windows11;

public abstract class Win11Strategy(ChooserOptions options): PromptStrategy {

    protected const int MIN_PIN_LENGTH = PinPolicy.MinLength;

    private static readonly Logger LOGGER = LogManager.GetLogger(typeof(Win11Strategy).FullName!);

    private static readonly   Condition CHOICES_LIST_CONDITION = new PropertyCondition(AutomationElement.ClassNameProperty, "ListView");
    protected static readonly Condition NEXT_BUTTON_CONDITION  = new PropertyCondition(AutomationElement.AutomationIdProperty, "OkButton");

    internal static TimeSpan AuthenticatorChoiceTimeout { get; set; } = TimeSpan.FromSeconds(30);

    internal static TimeSpan PinFieldTimeout { get; set; } = TimeSpan.FromMinutes(3);

    internal static Func<AutomationElement, CancellationToken, Task<IReadOnlyCollection<AutomationElement>?>>? FindChoicesOverride { get; set; }

    internal static Func<AutomationElement, CancellationToken, Task<AutomationElement?>>? FindPinOverride { get; set; }

    private IntPtr pinFilledHwnd;

    protected ChooserOptions options { get; } = options;

    public abstract bool canHandleTitle(string? actualTitle);
    public abstract Task handleWindow(string actualTitle, AutomationElement fidoEl, AutomationElement outerScrollViewer, bool isShiftDown);

    protected SkipReason getSkipReason(AutomationElement desiredChoice, IEnumerable<AutomationElement> authenticatorChoices, bool isShiftDown) {
        bool enabled = options.enabled;
        if (!enabled) {
            LOGGER.Info("Paused, not submitting dialog box");
            options.state.Report(ChooserEventKind.Paused, "Paused, not submitting dialog box");
            return SkipReason.Paused;
        }

        if (isShiftDown) {
            LOGGER.Info("Shift is pressed, not submitting dialog box");
            options.state.Report(ChooserEventKind.ShiftHeld, "Shift is pressed, not submitting dialog box");
            return SkipReason.ShiftHeld;
        }

        IReadOnlyList<ChoiceMatch> matches = authenticatorChoices.Select(choice => new ChoiceMatch(choice, choice.Current.Name)).ToList();
        LearnVisible(matches);
        AuthenticatorDecision decision = AuthenticatorPriorityPolicy.Decide(
            matches,
            options.priorityRules,
            options.LocaleNames(),
            options.skipAllNonSecurityKeyOptions);
        SkipReason reason = decision.Kind switch {
            AuthenticatorDecisionKind.Select => SkipReason.None,
            AuthenticatorDecisionKind.Ask => SkipReason.ExtraOptions,
            AuthenticatorDecisionKind.None => SkipReason.ExtraOptions,
            _ => throw new InvalidOperationException($"Unhandled authenticator decision {decision.Kind}")
        };
        if (reason == SkipReason.ExtraOptions) {
            LOGGER.Info(
                "Not auto-submitting this authenticator list ({0})",
                decision.Reason);
            options.state.Report(ChooserEventKind.ExtraOptions, "Other authenticator options are present; not auto-submitting");
        }

        return reason;
    }

    protected AuthenticatorDecision DecideVisible(IEnumerable<AutomationElement> authenticatorChoices) {
        IReadOnlyList<ChoiceMatch> matches = authenticatorChoices.Select(choice => new ChoiceMatch(choice, choice.Current.Name)).ToList();
        LearnVisible(matches);
        return AuthenticatorPriorityPolicy.Decide(
            matches,
            options.priorityRules,
            options.LocaleNames(),
            options.skipAllNonSecurityKeyOptions);
    }

    protected void LearnVisible(IReadOnlyList<ChoiceMatch> matches) {
        AppSettings next = AuthenticatorPriorityPolicy.Learn(
            options.state.ToSettings(),
            matches.Select(match => match.Name),
            options.LocaleNames());
        if (next.PriorityRules.Count == options.state.PriorityRules.Count) {
            return;
        }

        options.state.ApplySettings(next);
        options.persist?.Invoke();
    }

    protected bool shouldSkipSubmission(AutomationElement desiredChoice, IEnumerable<AutomationElement> authenticatorChoices, bool isShiftDown) =>
        getSkipReason(desiredChoice, authenticatorChoices, isShiftDown) != SkipReason.None;

    protected static async Task<IReadOnlyCollection<AutomationElement>?> findAuthenticatorChoices(AutomationElement outerScrollViewer, CancellationToken ct = default) {
        if (FindChoicesOverride is not null) {
            return await FindChoicesOverride(outerScrollViewer, ct);
        }

        using CancellationTokenSource stopFinding = CancellationTokenSource.CreateLinkedTokenSource(Startup.EXITING, ct);
        IReadOnlyList<AutomationElement>? authenticatorChoices =
            await outerScrollViewer.WaitForFirstAsync(TreeScope.Children, CHOICES_LIST_CONDITION, el => Task.FromResult(el.Children().ToList()), AuthenticatorChoiceTimeout, stopFinding.Token);
        if (authenticatorChoices == null) {
            LOGGER.Warn("Could not find authenticator choices after retrying for 1 minute. Giving up and not automatically selecting Security Key.");
        }
        return authenticatorChoices;
    }

    protected static AutomationElement? getSecurityKeyChoice(IEnumerable<AutomationElement> authenticatorChoices) {
        return authenticatorChoices.FirstOrDefault(choice => choice.nameContainsAny(I18N.getStrings(I18N.Key.SECURITY_KEY)));
    }

    protected static async Task<AutomationElement?> findPinField(AutomationElement outerScrollViewer, CancellationToken ct) {
        if (FindPinOverride is not null) {
            return await FindPinOverride(outerScrollViewer, ct);
        }

        return await outerScrollViewer.WaitForFirstAsync(TreeScope.Descendants, new PropertyCondition(AutomationElement.IsPasswordProperty, true),
            PinFieldTimeout, ct);
    }

    protected void handlePinPrompt(AutomationElement fidoEl, AutomationElement outerScrollViewer, AutomationElement? pinField = null) {
        IntPtr hwnd = new(fidoEl.Current.NativeWindowHandle);
        int pid = fidoEl.Current.ProcessId;
        PinFillDecision decision = PinFillPolicy.Decide(
            options.pinMode,
            options.pinCache?.HasCached == true,
            options.windowTrust.IsTrustedFidoWindow(hwnd, pid),
            options.devices.CountCtapHid(),
            pinFilledHwnd == hwnd && hwnd != IntPtr.Zero,
            options.debugger.IsAttached);

        switch (decision) {
            case PinFillDecision.DoNothing:
                return;
            case PinFillDecision.WatchLength:
                autosubmitPin(fidoEl, outerScrollViewer, pinField);
                return;
            case PinFillDecision.RefuseAndClear:
                LOGGER.Info("Clearing the cached PIN after a repeated PIN prompt or debugger");
                options.pinCache?.Clear();
                pinFilledHwnd = hwnd;
                return;
            case PinFillDecision.ManualFallback:
                LOGGER.Debug("Cached PIN fill is unavailable; leaving the PIN dialog for the user");
                return;
            case PinFillDecision.FillCache:
                if (options.pinCache is null) {
                    return;
                }

                if (!options.windowTrust.IsTrustedFidoWindow(hwnd, pid)) {
                    LOGGER.Warn("Refusing PIN fill because the FIDO window is no longer trusted");
                    return;
                }

                bool filled = options.pinCache.TryUse(bstr => options.pinFiller.TrySetPasswordValue(hwnd, bstr));
                if (!filled) {
                    LOGGER.Info("Native PIN fill failed; leaving the PIN dialog for the user");
                    return;
                }

                pinFilledHwnd = hwnd;
                if (!PinAutosubmit.TryInvokeOk(fidoEl)) {
                    LOGGER.Error("Could not invoke OK after filling the security key PIN");
                }

                return;
            default:
                throw new InvalidOperationException($"Unhandled PIN fill decision {decision}");
        }
    }

    protected void autosubmitPin(AutomationElement fidoEl, AutomationElement outerScrollViewer, AutomationElement? pinField = null) {
        bool cleaned = false;
        CancellationTokenSource windowClosed = new();
        Automation.AddAutomationEventHandler(WindowPattern.WindowClosedEvent, fidoEl, TreeScope.Element, cleanUp);

        Task.Run(async () => {
            try {
                LOGGER.Debug("Waiting for security key PIN prompt to appear");
                pinField ??= await findPinField(outerScrollViewer, windowClosed.Token);

                if (pinField != null) {
                    AutomationElement watched = pinField;
                    try {
                        Automation.AddAutomationPropertyChangedEventHandler(watched, TreeScope.Element, onPinTyped, ValuePattern.ValueProperty);
                    } catch (Exception exception) when (exception is not OutOfMemoryException) {
                        LOGGER.Debug("PIN field does not raise ValuePattern events; polling length instead");
                    }

                    considerLength(PinAutosubmit.TryReadLength(watched));
                    LOGGER.Debug("Found security key PIN prompt, waiting for the user to type {0:N0} characters before submitting it", options.autoSubmitPinLength);

                    while (!windowClosed.IsCancellationRequested) {
                        await Task.Delay(80, windowClosed.Token);
                        considerLength(PinAutosubmit.TryReadLength(watched));
                    }
                } else {
                    LOGGER.Debug("No security key PIN prompt found");
                }
            } catch (OperationCanceledException) {
                // PIN window closed or process exiting
            }
        }, windowClosed.Token);

        void onPinTyped(object sender, AutomationPropertyChangedEventArgs e) => considerLength(PinAutosubmit.LengthOfUiaValue(e.NewValue));

        void considerLength(int typedPinLength) {
            try {
                if (!options.enabled) {
                    return;
                }

                if (!PinPolicy.ShouldSubmitTypedPin(typedPinLength, options.autoSubmitPinLength)) {
                    return;
                }

                LOGGER.Info("Submitting security key PIN prompt because the user typed {0:N0} characters", typedPinLength);
                cleanUp();
                if (!PinAutosubmit.TryInvokeOk(fidoEl)) {
                    LOGGER.Error("Could not invoke OK on the security key PIN prompt");
                }
            } catch (Exception exception) when (exception is not OutOfMemoryException) {
                LOGGER.Error(exception);
            }
        }

        void cleanUp(object? sender = null, AutomationEventArgs? e = null) {
            if (cleaned) {
                return;
            }

            cleaned = true;
            try {
                if (pinField is not null) {
                    Automation.RemoveAutomationPropertyChangedEventHandler(pinField, onPinTyped);
                }
            } catch (ArgumentException) {
                // handler already removed
            }

            try {
                Automation.RemoveAutomationEventHandler(WindowPattern.WindowClosedEvent, fidoEl, cleanUp);
            } catch (ArgumentException) {
                // handler already removed
            }

            windowClosed.Cancel();
            windowClosed.Dispose();
            if (sender != null) {
                LOGGER.Debug("Security key PIN window closed");
            }
        }
    }

}