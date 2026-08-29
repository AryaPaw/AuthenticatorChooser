namespace AuthenticatorChooser.Fido;

public interface SecurityKeyChooser<in WINDOW> {

    void chooseUsbSecurityKey(WINDOW fidoPrompt);

}