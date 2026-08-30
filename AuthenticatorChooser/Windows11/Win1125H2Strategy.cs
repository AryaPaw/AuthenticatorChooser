using NLog;
using System.Windows.Automation;
using Unfucked;

namespace AuthenticatorChooser.Windows11;

public class Win1125H2Strategy(ChooserOptions options): Win11Strategy(options) {

    private static readonly Logger LOGGER = LogManager.GetLogger(typeof(Win1125H2Strategy).FullName!);

    private static readonly Condition LINK_CONDITION = new AndCondition(
        new PropertyCondition(AutomationElement.ClassNameProperty, "Hyperlink"),
        AutomationElement.NameProperty.singletonSafeCondition(false, I18N.getStrings(I18N.Key.CHOOSE_A_DIFFERENT_PASSKEY)));

    private static readonly Condition AUTHENTICATOR_NAME_CONDITION = new AndCondition(
        new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"),
        new PropertyCondition(AutomationElement.HeadingLevelProperty, AutomationHeadingLevel.None));

    public override bool canHandleTitle(string? actualTitle) =>
        TitlePolicy.CanHandleWin1125H2(
            actualTitle,
            options.wantsAggressiveTitles,
            I18N.getStrings(I18N.Key.CHOOSE_A_PASSKEY),
            I18N.getStrings(I18N.Key.SIGN_IN_WITH_A_PASSKEY));

    public override async Task handleWindow(string actualTitle, AutomationElement fidoEl, AutomationElement outerScrollViewer, bool isShiftDown) {
        if (Win1125H2ChallengePolicy.IsChooseAPasskeyTitle(actualTitle, I18N.getStrings(I18N.Key.CHOOSE_A_PASSKEY))) {
            if (await findAuthenticatorChoices(outerScrollViewer) is not { } authenticatorChoices) return;

            AuthenticatorDecision priority = DecideVisible(authenticatorChoices);
            switch (priority.Kind) {
                case AuthenticatorDecisionKind.None:
                    LOGGER.Debug("Desired choice not found, skipping");
                    options.state.Report(ChooserEventKind.DesiredChoiceMissing, "Desired choice not found, skipping");
                    return;
                case AuthenticatorDecisionKind.Ask:
                    if (isShiftDown) {
                        LOGGER.Info("Shift is pressed, not submitting dialog box");
                        options.state.Report(ChooserEventKind.ShiftHeld, "Shift is pressed, not submitting dialog box");
                        return;
                    }

                    if (!options.enabled) {
                        LOGGER.Info("Paused, not submitting dialog box");
                        options.state.Report(ChooserEventKind.Paused, "Paused, not submitting dialog box");
                        return;
                    }

                    options.state.Report(ChooserEventKind.ExtraOptions, "Other authenticator options are present; not auto-submitting");
                    return;
                case AuthenticatorDecisionKind.Select:
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled authenticator decision {priority.Kind}");
            }

            if (priority.Choice?.Id is not AutomationElement desiredChoice) {
                LOGGER.Debug("Desired choice not found, skipping");
                options.state.Report(ChooserEventKind.DesiredChoiceMissing, "Desired choice not found, skipping");
                return;
            }

            if (!shouldSkipSubmission(desiredChoice, authenticatorChoices, isShiftDown)) {
                ((SelectionItemPattern) desiredChoice.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
                LOGGER.Info("Choice selected {0:N3} sec after dialog appeared", options.overallStopwatch.Elapsed.TotalSeconds);
                options.state.Report(ChooserEventKind.ChoseSecurityKey, "Security key selected");
            }
        } else {
            if (!options.enabled) {
                LOGGER.Info("Paused, not submitting dialog box");
                options.state.Report(ChooserEventKind.Paused, "Paused, not submitting dialog box");
                return;
            }

            if (isShiftDown) {
                LOGGER.Info("Shift is pressed, not submitting dialog box");
                options.state.Report(ChooserEventKind.ShiftHeld, "Shift is pressed, not submitting dialog box");
                return;
            }

            if (await outerScrollViewer.WaitForFirstAsync(TreeScope.Children, AUTHENTICATOR_NAME_CONDITION) is not { } authenticatorNameEl) {
                LOGGER.Debug("Could not find name of the current authenticator while trying to skip a non-security-key option, ignoring dialog");
                return;
            }

            bool skipNonKey = options.skipAllNonSecurityKeyOptions
                || AuthenticatorPriorityCatalog.ActionFor(options.priorityRules, AuthenticatorPriorityCatalog.WindowsHelloId) == AuthenticatorRuleAction.Ignore;
            Win1125H2ChallengeAction action = Win1125H2ChallengePolicy.DecideChallenge(
                authenticatorNameEl.Current.Name,
                I18N.getStrings(I18N.Key.SECURITY_KEY),
                options.pinMode,
                options.autoSubmitPinLength,
                skipNonKey);

            switch (action) {
                case Win1125H2ChallengeAction.IgnoreMissingName:
                case Win1125H2ChallengeAction.LeaveAlone:
                    return;
                case Win1125H2ChallengeAction.AutosubmitSecurityKeyPin:
                    handlePinPrompt(fidoEl, outerScrollViewer);
                    return;
                case Win1125H2ChallengeAction.AlreadySecurityKey:
                    LOGGER.Debug("The current authenticator is already a security key, so there is nothing to do on this dialog");
                    return;
                case Win1125H2ChallengeAction.InvokeChooseDifferentPasskey:
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled 25H2 challenge action {action}");
            }

            if (outerScrollViewer.FindFirst(TreeScope.Children, LINK_CONDITION) is not { } chooseADifferentPasskeyLink) {
                LOGGER.Warn("Could not find 'Choose a different passkey' link in dialog");
                return;
            }

            ((InvokePattern) chooseADifferentPasskeyLink.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            LOGGER.Info("Requested list of all authenticators {0:N3} sec after dialog appeared", options.overallStopwatch.Elapsed.TotalSeconds);
        }
    }

}