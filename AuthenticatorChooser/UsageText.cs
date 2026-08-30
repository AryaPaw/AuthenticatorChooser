namespace AuthenticatorChooser;

public static class UsageText {

    public static string Build(string processFilename, string defaultLogPath) =>
        $"""
        {processFilename}
            Starts in the notification area (tray) and waits for Windows FIDO/WebAuthn prompts so it can choose the USB security key. Open the window from the tray icon if you want to pause or change options.

        {processFilename} --autostart-on-logon
            Registers this program to start automatically every time the current user logs on to Windows, and also leaves it running in the background like the first example.

        {processFilename} --skip-all-non-security-key-options
            Forces this program to choose the Security Key option even if there are other valid options, such as an already-paired phone or Windows Hello PIN or biometrics. By default, without this option, it will only choose the Security Key if the sole other option is pairing a new phone. This is an aggressive behavior, so if it skips an option you need, remember that you can hold Shift when the FIDO prompt appears to temporarily disable this program and manually choose a different option.

        {processFilename} --autosubmit-pin-length=$num
            When Windows prompts you for the FIDO PIN for your USB security key, automatically submit the dialog once you have typed a PIN that is $num characters long (minimum 4), instead of you manually pressing Enter. Remember that enough consecutive incorrect submissions (8 on YubiKeys) will permanently block the security key until you reset it and lose all its FIDO credentials, so type with care. This will neither autosubmit PINs when registering a new FIDO credential, changing your PIN, or entering a Windows Hello PIN (which Windows autosubmits without this program's help).

        {processFilename} --log[=$filename]
            Runs this program in the background like the first example, and logs debug messages to a text file. If you don't specify $filename, it goes to {defaultLogPath}.

        {processFilename} --show-window
            Starts like the first example, but opens the status window immediately. Used by local preview (`scripts/run-local.ps1`).

        {processFilename} --help
            Shows this usage.

        {processFilename} --uninstall-cleanup
            Removes this user's scheduled task, startup registry value, and %AppData%\AuthenticatorChooser. Used by the uninstaller.

        For more information, see https://github.com/AryaPaw/AuthenticatorChooser.
        Press Ctrl+C to copy this message.
        """;

    public static string DefaultLogPath => Path.Combine(SettingsStore.DefaultDirectory, nameof(AuthenticatorChooser) + ".log");

}
