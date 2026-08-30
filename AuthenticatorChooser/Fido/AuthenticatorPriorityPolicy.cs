namespace AuthenticatorChooser.Fido;

public readonly record struct LocaleAuthenticatorNames(
    IReadOnlyList<string> SecurityKey,
    IReadOnlyList<string> PairNewPhone,
    IReadOnlyList<string> WindowsHello);

public enum AuthenticatorDecisionKind {
    Select,
    Ask,
    None
}

public readonly record struct AuthenticatorDecision(AuthenticatorDecisionKind Kind, ChoiceMatch? Choice, string Reason);

public static class AuthenticatorPriorityPolicy {

    public static AuthenticatorKind Classify(string name, LocaleAuthenticatorNames locale, IEnumerable<AuthenticatorPriorityRule> rules) {
        if (ChoiceMatchPolicy.NameContainsAny(name, locale.SecurityKey)) {
            return AuthenticatorKind.Usb;
        }

        if (ChoiceMatchPolicy.NameContainsAny(name, locale.PairNewPhone)) {
            return AuthenticatorKind.PairNewPhone;
        }

        if (ChoiceMatchPolicy.NameContainsAny(name, locale.WindowsHello)) {
            return AuthenticatorKind.WindowsHello;
        }

        foreach (AuthenticatorPriorityRule rule in rules) {
            if (rule.Kind == AuthenticatorKind.External
                && string.Equals(rule.DisplayName, name, StringComparison.OrdinalIgnoreCase)) {
                return AuthenticatorKind.External;
            }
        }

        return AuthenticatorKind.External;
    }

    public static AuthenticatorDecision Decide(
        IReadOnlyList<ChoiceMatch> visible,
        IReadOnlyList<AuthenticatorPriorityRule> rules,
        LocaleAuthenticatorNames locale,
        bool skipAllUnknownAsIgnore) {
        List<AuthenticatorPriorityRule> catalog = AuthenticatorPriorityCatalog.EnsureBuiltIns(rules);
        List<(ChoiceMatch Choice, AuthenticatorKind Kind, AuthenticatorPriorityRule? Rule)> classified = [];
        foreach (ChoiceMatch choice in visible) {
            AuthenticatorKind kind = Classify(choice.Name, locale, catalog);
            AuthenticatorPriorityRule? rule = FindRule(choice.Name, kind, catalog);
            classified.Add((choice, kind, rule));
        }

        if (!skipAllUnknownAsIgnore) {
            if (classified.Any(item => item.Rule is null)) {
                return new AuthenticatorDecision(AuthenticatorDecisionKind.Ask, null, "Unknown authenticator is present");
            }

            (ChoiceMatch Choice, AuthenticatorKind Kind, AuthenticatorPriorityRule? Rule)? ask = classified
                .FirstOrDefault(item => item.Rule is { Action: AuthenticatorRuleAction.Ask });
            if (ask is { Rule: not null }) {
                return new AuthenticatorDecision(AuthenticatorDecisionKind.Ask, ask.Value.Choice, "A visible option is set to Ask");
            }
        }

        foreach (AuthenticatorPriorityRule rule in catalog) {
            if (rule.Action == AuthenticatorRuleAction.Ignore) {
                continue;
            }

            if (skipAllUnknownAsIgnore && rule.Action == AuthenticatorRuleAction.Ask) {
                continue;
            }

            ChoiceMatch? match = classified
                .Where(item => Matches(item.Choice.Name, item.Kind, rule))
                .Select(item => (ChoiceMatch?) item.Choice)
                .FirstOrDefault();
            if (match is null) {
                continue;
            }

            return rule.Action switch {
                AuthenticatorRuleAction.Select => new AuthenticatorDecision(AuthenticatorDecisionKind.Select, match, "Matched Select rule"),
                AuthenticatorRuleAction.Ask => new AuthenticatorDecision(AuthenticatorDecisionKind.Ask, match, "Matched Ask rule"),
                AuthenticatorRuleAction.Ignore => new AuthenticatorDecision(AuthenticatorDecisionKind.None, null, "Ignore rules are skipped"),
                _ => throw new InvalidOperationException($"Unhandled authenticator rule action {rule.Action}")
            };
        }

        return new AuthenticatorDecision(AuthenticatorDecisionKind.None, null, "No matching Select rule");
    }

    public static AppSettings Learn(AppSettings settings, IEnumerable<string> visibleNames, LocaleAuthenticatorNames locale) {
        List<AuthenticatorPriorityRule> rules = AuthenticatorPriorityCatalog.EnsureBuiltIns(settings.PriorityRules);
        bool changed = false;
        foreach (string name in visibleNames) {
            if (string.IsNullOrWhiteSpace(name)) {
                continue;
            }

            AuthenticatorKind kind = Classify(name, locale, rules);
            if (kind != AuthenticatorKind.External) {
                continue;
            }

            if (FindRule(name, kind, rules) is not null) {
                continue;
            }

            rules.Add(new AuthenticatorPriorityRule {
                Id = "learned:" + Guid.NewGuid().ToString("N"),
                Kind = AuthenticatorKind.External,
                DisplayName = name.Trim(),
                Action = AuthenticatorRuleAction.Ask,
                BuiltIn = false
            });
            changed = true;
        }

        if (!changed) {
            return settings;
        }

        AppSettings next = SettingsStore.Clone(settings);
        next.PriorityRules = rules;
        return next;
    }

    private static AuthenticatorPriorityRule? FindRule(string name, AuthenticatorKind kind, IEnumerable<AuthenticatorPriorityRule> rules) {
        foreach (AuthenticatorPriorityRule rule in rules) {
            if (Matches(name, kind, rule)) {
                return rule;
            }
        }

        return null;
    }

    private static bool Matches(string name, AuthenticatorKind kind, AuthenticatorPriorityRule rule) {
        if (rule.Kind != kind) {
            return false;
        }

        if (kind == AuthenticatorKind.External) {
            return string.Equals(rule.DisplayName, name, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

}
