namespace AuthenticatorChooser.Fido;

public enum AuthenticatorKind {
    Usb,
    PairNewPhone,
    WindowsHello,
    External
}

public enum AuthenticatorRuleAction {
    Select,
    Ask,
    Ignore
}

public enum PriorityNameAddStatus {
    Added,
    Empty,
    Duplicate
}
