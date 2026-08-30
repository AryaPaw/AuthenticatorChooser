namespace AuthenticatorChooser.Fido;

public static class AuthenticatorPriorityCatalog {

    public const string UsbId = "builtin:usb";
    public const string PairNewPhoneId = "builtin:pair-phone";
    public const string WindowsHelloId = "builtin:windows-hello";

    public static IReadOnlyList<AuthenticatorPriorityRule> CreateDefaults() => [
        new() {
            Id = UsbId,
            Kind = AuthenticatorKind.Usb,
            DisplayName = "USB security key",
            Action = AuthenticatorRuleAction.Select,
            BuiltIn = true
        },
        new() {
            Id = PairNewPhoneId,
            Kind = AuthenticatorKind.PairNewPhone,
            DisplayName = "Pair a new phone",
            Action = AuthenticatorRuleAction.Ignore,
            BuiltIn = true
        },
        new() {
            Id = WindowsHelloId,
            Kind = AuthenticatorKind.WindowsHello,
            DisplayName = "This Windows device",
            Action = AuthenticatorRuleAction.Ask,
            BuiltIn = true
        }
    ];

    public static List<AuthenticatorPriorityRule> Clone(IEnumerable<AuthenticatorPriorityRule>? rules) =>
        (rules ?? []).Select(rule => rule.Clone()).ToList();

    public static List<AuthenticatorPriorityRule> EnsureBuiltIns(IEnumerable<AuthenticatorPriorityRule>? rules) {
        List<AuthenticatorPriorityRule> next = Clone(rules);
        foreach (AuthenticatorPriorityRule builtin in CreateDefaults()) {
            AuthenticatorPriorityRule? existing = next.FirstOrDefault(rule => rule.Id == builtin.Id);
            if (existing is null) {
                next.Insert(IndexForBuiltin(next, builtin.Id), builtin);
                continue;
            }

            existing.Kind = builtin.Kind;
            existing.BuiltIn = true;
            if (string.IsNullOrWhiteSpace(existing.DisplayName)) {
                existing.DisplayName = builtin.DisplayName;
            }
        }

        return next.Count == 0 ? Clone(CreateDefaults()) : next;
    }

    public static List<AuthenticatorPriorityRule> ApplySkipAll(IEnumerable<AuthenticatorPriorityRule>? rules) {
        List<AuthenticatorPriorityRule> next = EnsureBuiltIns(rules);
        foreach (AuthenticatorPriorityRule rule in next) {
            if (rule.Id == UsbId) {
                rule.Action = AuthenticatorRuleAction.Select;
                continue;
            }

            rule.Action = AuthenticatorRuleAction.Ignore;
        }

        return next;
    }

    public static AuthenticatorRuleAction ActionFor(IEnumerable<AuthenticatorPriorityRule> rules, string id) {
        AuthenticatorPriorityRule? match = rules.FirstOrDefault(rule => rule.Id == id);
        return match?.Action ?? AuthenticatorRuleAction.Ask;
    }

    public static string Summary(IEnumerable<AuthenticatorPriorityRule> rules) {
        List<AuthenticatorPriorityRule> list = rules.ToList();
        string usb = ActionFor(list, UsbId).ToString();
        string phone = ActionFor(list, PairNewPhoneId).ToString();
        string hello = ActionFor(list, WindowsHelloId).ToString();
        int learned = list.Count(rule => !rule.BuiltIn);
        return $"USB: {usb}. Pair phone: {phone}. Windows Hello: {hello}. Learned names: {learned}.";
    }

    private static int IndexForBuiltin(List<AuthenticatorPriorityRule> rules, string id) {
        if (id == UsbId) {
            return 0;
        }

        if (id == PairNewPhoneId) {
            int usb = rules.FindIndex(rule => rule.Id == UsbId);
            return usb >= 0 ? usb + 1 : 0;
        }

        int phone = rules.FindIndex(rule => rule.Id == PairNewPhoneId);
        if (phone >= 0) {
            return phone + 1;
        }

        int usbIndex = rules.FindIndex(rule => rule.Id == UsbId);
        return usbIndex >= 0 ? usbIndex + 1 : 0;
    }

}
