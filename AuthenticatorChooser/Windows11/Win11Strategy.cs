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
    public abstract Task handleWindow(string actualTitle, AutomationElement fidoEl, AutomationElement outerScrollViewer, bool isShiftDown, IntPtr hostWindow = default);

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

        if (!PinFillPolicy.AllowsChoiceAutosubmit(options.pinMode)) {
            LOGGER.Info("PIN mode is off, not submitting dialog box");
            options.state.Report(ChooserEventKind.PinModeOff, "PIN mode is off, not submitting dialog box");
            return SkipReason.PinModeOff;
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

    protected void handlePinPrompt(AutomationElement fidoEl, AutomationElement outerScrollViewer, AutomationElement? pinField = null, IntPtr hostWindow = default) {
        IntPtr hwnd = PinLearnPolicy.ResolveDialogHwnd(new(fidoEl.Current.NativeWindowHandle), hostWindow);
        int pid = fidoEl.Current.ProcessId;
        if (hwnd != IntPtr.Zero) {
            NativeSecurity.GetWindowThreadProcessId(hwnd, out uint windowPid);
            if (windowPid != 0) {
                pid = (int) windowPid;
            }
        }

        bool trusted = options.windowTrust.IsTrustedFidoWindow(hwnd, pid);
        LOGGER.Info("PIN dialog hwnd=0x{hwnd:x} pid={pid} trusted={trusted}", hwnd.ToInt64(), pid, trusted);
        PinFillDecision decision = PinFillPolicy.Decide(
            options.pinMode,
            options.pinCache?.HasCached == true,
            trusted,
            options.devices.CountCtapHid(),
            pinFilledHwnd == hwnd && hwnd != IntPtr.Zero,
            options.debugger.IsAttached);
        LOGGER.Info("PIN fill decision {decision}", decision);

        switch (decision) {
            case PinFillDecision.DoNothing:
                return;
            case PinFillDecision.WatchLength:
                autosubmitPin(fidoEl, outerScrollViewer, pinField);
                return;
            case PinFillDecision.RefuseAndClear:
                LOGGER.Info("Clearing the cached PIN after a repeated PIN prompt or debugger");
                options.pinCache?.Clear();
                ForgetLearnedPinLength();
                if (options.debugger.IsAttached) {
                    pinFilledHwnd = hwnd;
                    return;
                }

                pinFilledHwnd = IntPtr.Zero;
                learnPin(fidoEl, hwnd, pid, outerScrollViewer, pinField);
                return;
            case PinFillDecision.ManualFallback:
                LOGGER.Info("Cached PIN fill is unavailable; leaving the PIN dialog for the user");
                return;
            case PinFillDecision.FillCache:
                if (options.pinCache is null) {
                    return;
                }

                if (!options.windowTrust.IsTrustedFidoWindow(hwnd, pid)) {
                    LOGGER.Warn("Refusing PIN fill because the FIDO window is no longer trusted");
                    return;
                }

                fillCachedPin(fidoEl, outerScrollViewer, pinField, hwnd, pid);
                return;
            case PinFillDecision.LearnFromPrompt:
                learnPin(fidoEl, hwnd, pid, outerScrollViewer, pinField);
                return;
            default:
                throw new InvalidOperationException($"Unhandled PIN fill decision {decision}");
        }
    }

    private void fillCachedPin(
        AutomationElement fidoEl,
        AutomationElement outerScrollViewer,
        AutomationElement? pinField,
        IntPtr hwnd,
        int pid) {
        _ = Task.Run(async () => {
            try {
                using CancellationTokenSource findTimeout = new(PinFillRetryPolicy.FindFieldTimeout);
                using CancellationTokenSource findCts = CancellationTokenSource.CreateLinkedTokenSource(Startup.EXITING, findTimeout.Token);
                try {
                    pinField ??= await findPinField(outerScrollViewer, findCts.Token);
                } catch (OperationCanceledException) when (!Startup.EXITING.IsCancellationRequested) {
                    pinField = null;
                }

                if (pinField is null) {
                    LOGGER.Info("PIN field not found in time; capturing typed keys instead");
                    if (PinFillPolicy.AfterFailedFill(options.pinMode) == PinFillDecision.LearnFromPrompt) {
                        learnPin(fidoEl, hwnd, pid, outerScrollViewer, null);
                    }

                    return;
                }

                if (!options.windowTrust.IsTrustedFidoWindow(hwnd, pid) || options.pinCache is null) {
                    LOGGER.Warn("Refusing PIN fill because the FIDO window is no longer trusted");
                    return;
                }

                IntPtr fieldHwnd = new(pinField.Current.NativeWindowHandle);
                AutomationElement field = pinField;
                bool filled = false;
                foreach (int delayMs in PinFillRetryPolicy.DelayMs) {
                    if (delayMs > 0) {
                        await Task.Delay(delayMs, Startup.EXITING);
                    }

                    if (!options.windowTrust.IsTrustedFidoWindow(hwnd, pid) || options.pinCache is null) {
                        LOGGER.Warn("Refusing PIN fill because the FIDO window is no longer trusted");
                        return;
                    }

                    try {
                        fieldHwnd = new(field.Current.NativeWindowHandle);
                    } catch (ElementNotAvailableException) {
                        LOGGER.Info("PIN dialog disappeared before it could be filled");
                        return;
                    }

                    IntPtr foreground = NativeSecurity.GetForegroundWindow();
                    filled = options.pinCache.TryUse(bstr => TryFillPin(hwnd, fieldHwnd, foreground, field, bstr));
                    if (filled) {
                        break;
                    }
                }

                if (!filled) {
                    LOGGER.Info("PIN fill failed; leaving the PIN dialog for the user (cache kept)");
                    if (PinFillPolicy.AfterFailedFill(options.pinMode) == PinFillDecision.LearnFromPrompt) {
                        learnPin(fidoEl, hwnd, pid, outerScrollViewer, field);
                    }

                    return;
                }

                pinFilledHwnd = hwnd;
                LOGGER.Info("Filled the security key PIN from the in-process cache");
                if (!PinAutosubmit.TryInvokeOk(fidoEl)) {
                    LOGGER.Error("Could not invoke OK after filling the security key PIN");
                }
            } catch (ElementNotAvailableException) {
                LOGGER.Info("PIN dialog disappeared before it could be filled");
            } catch (OperationCanceledException) {
            } catch (Exception exception) when (exception is not OutOfMemoryException) {
                LOGGER.Error(exception, "PIN fill failed");
            }
        });
    }

    private bool TryFillPin(IntPtr hwnd, IntPtr fieldHwnd, IntPtr foreground, AutomationElement field, IntPtr bstr) {
        IntPtr hostRoot = NativeSecurity.GetAncestorRoot(hwnd);
        IntPtr ownerRoot = NativeSecurity.GetAncestorOwnerRoot(hwnd);
        foreach (IntPtr target in PinFillHwndPolicy.SearchOrder(hwnd, fieldHwnd, foreground, hostRoot, ownerRoot)) {
            if (options.pinFiller.TrySetPasswordValue(target, bstr)) {
                return true;
            }
        }

        return PinAutosubmit.TrySetValue(field, bstr);
    }

    private void learnPin(AutomationElement fidoEl, IntPtr hwnd, int pid, AutomationElement outerScrollViewer, AutomationElement? pinField) {
        if (options.pinCache is null) {
            return;
        }

        if (hwnd == IntPtr.Zero && pid <= 0) {
            LOGGER.Warn("Cannot learn the security key PIN because the dialog window handle is missing");
            return;
        }

        PinLearnSession session = new();
        options.pinKeyHook.Stop();
        options.pinKeyHook.Start(hwnd, pid, session);
        int? learned = options.learnedPinLength;
        if (PinCacheUxPolicy.AutosubmitFirstTypedPin(learned)) {
            LOGGER.Info("Waiting for the user to type {0:N0} characters; OK will be pressed and the PIN cached in this process", learned);
        } else {
            LOGGER.Info("Waiting for the user to type the security key PIN once; it will be cached in this process only");
        }

        options.state.Report(ChooserEventKind.Waiting, PinCacheUxPolicy.WaitingStatus(learned));

        bool cleaned = false;
        bool submitted = false;
        object commitGate = new();
        CancellationTokenSource windowClosed = new();
        Automation.AddAutomationEventHandler(WindowPattern.WindowClosedEvent, fidoEl, TreeScope.Element, onWindowClosed);

        _ = WatchPinField(outerScrollViewer, pinField, session, windowClosed.Token, CommitIfReady, TryAutosubmit);
        _ = WatchHostWindow(hwnd, windowClosed.Token, CommitIfReady);

        void onWindowClosed(object? sender, AutomationEventArgs e) => CommitIfReady();

        void TryAutosubmit(int typedLength) {
            if (typedLength == 0) {
                submitted = false;
                return;
            }

            if (submitted || !options.enabled) {
                return;
            }

            if (!PinCacheUxPolicy.AutosubmitFirstTypedPin(options.learnedPinLength)) {
                return;
            }

            if (!PinPolicy.ShouldSubmitTypedPin(typedLength, options.learnedPinLength)) {
                return;
            }

            submitted = true;
            LOGGER.Info("Submitting the security key PIN after {0:N0} characters; it will be cached in this process", typedLength);
            if (!PinAutosubmit.TryInvokeOk(fidoEl)) {
                submitted = false;
                LOGGER.Error("Could not invoke OK after the typed PIN reached the saved length");
            }
        }

        void CommitIfReady() {
            lock (commitGate) {
                if (cleaned) {
                    return;
                }

                cleaned = true;
            }
            try {
                Automation.RemoveAutomationEventHandler(WindowPattern.WindowClosedEvent, fidoEl, onWindowClosed);
            } catch (ArgumentException) {
            }

            windowClosed.Cancel();
            windowClosed.Dispose();
            options.pinKeyHook.Stop();
            int captured = session.CapturedLength;
            string? pin = session.TakeCommitOnWindowClosed();
            if (pin is null) {
                LOGGER.Info("PIN prompt closed without a cacheable PIN (captured {count} characters)", captured);
                return;
            }

            PinCacheStoreResult stored = options.pinCache.TryStore(pin);
            if (stored == PinCacheStoreResult.Stored) {
                RememberPinLength(pin.Length);
                LOGGER.Info("Cached the security key PIN in this process after the Windows prompt closed");
            } else {
                LOGGER.Info("Did not cache the typed PIN ({result})", stored);
            }
        }
    }

    private void ForgetLearnedPinLength() {
        if (options.state.LearnedPinLength is null) {
            return;
        }

        options.state.LearnedPinLength = null;
        options.persist?.Invoke();
        LOGGER.Info("Forgot remembered PIN length after a repeated prompt");
    }

    private void RememberPinLength(int pinLength) {
        int? remembered = PinCacheUxPolicy.RememberedLength(pinLength);
        if (remembered == options.state.LearnedPinLength) {
            return;
        }

        options.state.LearnedPinLength = remembered;
        options.persist?.Invoke();
        LOGGER.Info("Remembered PIN length {0:N0} from the Windows prompt", remembered);
    }

    private static async Task WatchPinField(
        AutomationElement outerScrollViewer,
        AutomationElement? pinField,
        PinLearnSession session,
        CancellationToken ct,
        Action commit,
        Action<int> onLength) {
        try {
            pinField ??= await findPinField(outerScrollViewer, ct);
            if (pinField is null) {
                return;
            }

            AutomationElement watched = pinField;
            int lastLength = 0;
            Observe(PinAutosubmit.TryReadLength(watched));
            while (!ct.IsCancellationRequested) {
                await Task.Delay(80, ct);
                int length;
                try {
                    length = PinAutosubmit.TryReadLength(watched);
                } catch (ElementNotAvailableException) {
                    if (session.CanCommit()) {
                        commit();
                    }

                    return;
                }

                Observe(length);
            }

            void Observe(int length) {
                onLength(length);
                if (lastLength > 0 && length == 0) {
                    session.OnFieldEmptied();
                }

                lastLength = length;
            }
        } catch (OperationCanceledException) {
        }
    }

    private static async Task WatchHostWindow(IntPtr hwnd, CancellationToken ct, Action commit) {
        if (hwnd == IntPtr.Zero) {
            return;
        }

        try {
            while (!ct.IsCancellationRequested && NativeSecurity.IsWindow(hwnd)) {
                await Task.Delay(100, ct);
            }

            if (!ct.IsCancellationRequested) {
                commit();
            }
        } catch (OperationCanceledException) {
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