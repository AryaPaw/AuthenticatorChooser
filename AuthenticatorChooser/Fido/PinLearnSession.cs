using System.Text;

namespace AuthenticatorChooser.Fido;

internal sealed class PinLearnSession {

    private readonly StringBuilder typed = new();
    private string? candidate;

    public void OnCharacter(char value) {
        if (char.IsControl(value)) {
            return;
        }

        typed.Append(value);
    }

    public void OnBackspace() {
        if (typed.Length > 0) {
            typed.Length--;
        }
    }

    public void OnEnter() {
        candidate = typed.ToString();
        typed.Clear();
    }

    public void OnFieldCleared() {
        typed.Clear();
        candidate = null;
    }

    public void OnFieldEmptied() {
        if (PinPolicy.ShouldAutosubmit(typed.Length)) {
            OnEnter();
        }
    }

    public int CapturedLength {
        get {
            string live = typed.ToString();
            if (PinPolicy.ShouldAutosubmit(live.Length)) {
                return live.Length;
            }

            return candidate?.Length ?? 0;
        }
    }

    public bool CanCommit() {
        string live = typed.ToString();
        string? pin = PinPolicy.ShouldAutosubmit(live.Length) ? live : candidate;
        return pin is not null && PinPolicy.ShouldAutosubmit(pin.Length);
    }

    public string? TakeCommitOnWindowClosed() {
        string live = typed.ToString();
        string? pin = PinPolicy.ShouldAutosubmit(live.Length) ? live : candidate;
        typed.Clear();
        candidate = null;
        if (pin is null || !PinPolicy.ShouldAutosubmit(pin.Length)) {
            return null;
        }

        return pin;
    }

}
