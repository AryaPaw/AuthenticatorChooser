using NLog;
using System.Windows.Automation;

namespace AuthenticatorChooser.Windows11;

public class Win1123H2Strategy(ChooserOptions options): Win11Strategy(options) {

    private static readonly Logger LOGGER = LogManager.GetLogger(typeof(Win1123H2Strategy).FullName!);

    public override bool canHandleTitle(string? actualTitle) =>
        TitlePolicy.CanHandleWin1123H2(
            actualTitle,
            TitlePolicy.IncludeAggressiveTitles(options.skipAllNonSecurityKeyOptions, options.autoSubmitPinLength),
            I18N.getStrings(I18N.Key.SIGN_IN_WITH_YOUR_PASSKEY),
            I18N.getStrings(I18N.Key.MAKING_SURE_ITS_YOU));

    /**
     * If we're on the TPM dialog, and the user wants to absolutely always use security keys, then we just selected "Use another device" to see the list of all authenticator choices, so the dialog is closing because we selected something, so don't do anything else with the soon to be nonexistent dialog.
     * Otherwise, perform common checks like holding Shift and stopping if there are other options.
     * Finally, click Next.
     */
    public override async Task handleWindow(string actualTitle, AutomationElement fidoEl, AutomationElement outerScrollViewer, bool isShiftDown) {
        CancellationTokenSource stopFinding = new();

        /*
         * If the TPM contains a passkey for this RP, Windows will ask for your fingerprint/PIN/face, and you have to select "Use another device" and click Next to see all the authenticator choices.
         * #5, #11: power series backoff, max=500 ms per attempt, ~1 minute total
         */
        Task<IReadOnlyCollection<AutomationElement>?> authenticatorChoicesTask = findAuthenticatorChoices(outerScrollViewer, stopFinding.Token);

        Task<AutomationElement?> pinFieldTask = options.autoSubmitPinLength >= MIN_PIN_LENGTH && I18N.getStrings(I18N.Key.MAKING_SURE_ITS_YOU).Contains(actualTitle, StringComparer.CurrentCulture)
            ? findPinField(outerScrollViewer, stopFinding.Token) : new TaskCompletionSource<AutomationElement?>().Task;

        await Task.WhenAny(authenticatorChoicesTask, pinFieldTask);
        await stopFinding.CancelAsync();

        if (pinFieldTask.IsCompletedSuccessfully) {
            if (pinFieldTask.Result is { } pinField) {
                LOGGER.Debug("Found PIN field");
                autosubmitPin(fidoEl, outerScrollViewer, pinField);
            }
            return;
        }
        if (authenticatorChoicesTask is not { IsCompletedSuccessfully: true, Result: { } authenticatorChoices }) {
            LOGGER.Warn("Could not find authenticator choices after retrying for 1 minute. Giving up and not automatically selecting Security Key.");
            return;
        }

        AutomationElement? desiredChoice = getSecurityKeyChoice(authenticatorChoices);
        bool securityKeyFound = desiredChoice != null;
        AutomationElement? useAnother = null;
        if (desiredChoice == null && options.skipAllNonSecurityKeyOptions) {
            useAnother = authenticatorChoices.FirstOrDefault(choice => choice.nameContainsAny(I18N.getStrings(I18N.Key.USE_ANOTHER_DEVICE)));
            desiredChoice = useAnother;
        }

        if (desiredChoice == null) {
            LOGGER.Debug("Desired choice not found, skipping");
            options.state.Report(ChooserEventKind.DesiredChoiceMissing, "Desired choice not found, skipping");
            return;
        }

        SkipReason skipReason = getSkipReason(desiredChoice, authenticatorChoices, isShiftDown);
        Win1123H2ListDecision decision = Win1123H2ListPolicy.Decide(
            securityKeyFound,
            useAnother != null,
            options.skipAllNonSecurityKeyOptions,
            isShiftDown,
            skipReason);

        if (decision.SelectChoice) {
            ((SelectionItemPattern) desiredChoice.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        }

        if (decision.IsLocalWindowsHelloTpmPrompt) {
            // prompt already closed or remains open because Shift is held
        } else if (fidoEl.FindFirst(TreeScope.Children, NEXT_BUTTON_CONDITION) is not { } nextButton) {
            LOGGER.Error("Could not find Next button in Windows Security dialog box, skipping this dialog box instance");
        } else if (decision.TrySubmitNext) {
            ((InvokePattern) nextButton.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            LOGGER.Info("Next button pressed {0:N3} sec after dialog appeared", options.overallStopwatch.Elapsed.TotalSeconds);
            options.state.Report(ChooserEventKind.ChoseSecurityKey, "Security key selected");
        }
    }

}