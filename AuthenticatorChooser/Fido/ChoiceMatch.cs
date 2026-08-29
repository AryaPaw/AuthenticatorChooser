namespace AuthenticatorChooser.Fido;

public readonly record struct ChoiceMatch(object Id, string Name);

public static class ChoiceMatchPolicy {

    public static bool NameContainsAny(string name, IEnumerable<string> possibleSubstrings) {
        return possibleSubstrings.Any(possibleSubstring => name.Contains(possibleSubstring, StringComparison.CurrentCulture));
    }

    public static bool IsOnlySecurityKeyAndNewPhone(IReadOnlyList<ChoiceMatch> choices, ChoiceMatch desired, IEnumerable<string> smartphoneSubstrings) {
        IEnumerable<string> phones = smartphoneSubstrings as IList<string> ?? smartphoneSubstrings.ToList();
        return choices.All(choice => ReferenceEquals(choice.Id, desired.Id) || NameContainsAny(choice.Name, phones));
    }

    public static ChoiceMatch? FindByNameSubstring(IEnumerable<ChoiceMatch> choices, IEnumerable<string> substrings) {
        IEnumerable<string> needles = substrings as IList<string> ?? substrings.ToList();
        foreach (ChoiceMatch choice in choices) {
            if (NameContainsAny(choice.Name, needles)) {
                return choice;
            }
        }

        return null;
    }

}
