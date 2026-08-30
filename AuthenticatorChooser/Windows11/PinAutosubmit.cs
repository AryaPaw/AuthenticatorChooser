using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace AuthenticatorChooser.Windows11;

internal static class PinAutosubmit {

    private static readonly Condition OkButtonCondition = new PropertyCondition(AutomationElement.AutomationIdProperty, "OkButton");

    public static int LengthOfUiaValue(object? newValue) {
        if (newValue is string text) {
            return text.Length;
        }

        if (newValue is null) {
            return 0;
        }

        return Convert.ToString(newValue, CultureInfo.InvariantCulture)?.Length ?? 0;
    }

    public static int TryReadLength(AutomationElement field) {
        try {
            object raw = field.GetCurrentPropertyValue(ValuePattern.ValueProperty, true);
            int ignoredDefault = LengthOfUiaValue(raw);
            if (ignoredDefault > 0) {
                return ignoredDefault;
            }
        } catch (ElementNotAvailableException) {
            return 0;
        } catch (InvalidOperationException) {
            // pattern unavailable; try the current pattern next
        }

        try {
            if (field.TryGetCurrentPattern(ValuePattern.Pattern, out object pattern)) {
                return ((ValuePattern) pattern).Current.Value?.Length ?? 0;
            }
        } catch (ElementNotAvailableException) {
            return 0;
        } catch (InvalidOperationException) {
            return 0;
        }

        return 0;
    }

    public static bool TryInvokeOk(AutomationElement fidoEl) {
        try {
            AutomationElement? ok = fidoEl.FindFirst(TreeScope.Children, OkButtonCondition)
                ?? fidoEl.FindFirst(TreeScope.Descendants, OkButtonCondition);
            if (ok is null || !ok.TryGetCurrentPattern(InvokePattern.Pattern, out object pattern)) {
                return false;
            }

            ((InvokePattern) pattern).Invoke();
            return true;
        } catch (ElementNotAvailableException) {
            return false;
        } catch (InvalidOperationException) {
            return false;
        }
    }

    public static bool TrySetValue(AutomationElement field, IntPtr bstrPin) {
        if (bstrPin == IntPtr.Zero) {
            return false;
        }

        string? pin = Marshal.PtrToStringBSTR(bstrPin);
        if (string.IsNullOrEmpty(pin)) {
            return false;
        }

        try {
            field.SetFocus();
        } catch (ElementNotAvailableException) {
            return false;
        } catch (InvalidOperationException) {
        }

        try {
            if (!field.TryGetCurrentPattern(ValuePattern.Pattern, out object pattern)) {
                return false;
            }

            ((ValuePattern) pattern).SetValue(pin);
            return true;
        } catch (ElementNotAvailableException) {
            return false;
        } catch (InvalidOperationException) {
            return false;
        }
    }

}
