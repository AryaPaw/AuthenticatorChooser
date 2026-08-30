namespace AuthenticatorChooser.Fido;

public sealed class AuthenticatorPriorityRule {

    public string Id { get; set; } = "";

    public AuthenticatorKind Kind { get; set; }

    public string DisplayName { get; set; } = "";

    public AuthenticatorRuleAction Action { get; set; }

    public bool BuiltIn { get; set; }

    public AuthenticatorPriorityRule Clone() => new() {
        Id = Id,
        Kind = Kind,
        DisplayName = DisplayName,
        Action = Action,
        BuiltIn = BuiltIn
    };

}
