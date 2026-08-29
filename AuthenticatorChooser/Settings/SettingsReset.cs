namespace AuthenticatorChooser.Settings;

internal static class SettingsReset {

    public static bool TryApply(
        AppState state,
        IAutostartService autostart,
        string executablePath,
        string settingsPath,
        string allowedRoot) {
        state.ApplySettings(new AppSettings());
        SettingsStore.EnsurePathAllowed(settingsPath, allowedRoot);
        SettingsStore.Save(settingsPath, state.ToSettings());
        Logging.initialize(state.FileLogEnabled, state.LogFilename);
        if (state.AutostartOnLogon) {
            return autostart.Register(executablePath, null);
        }

        return autostart.Unregister();
    }

}
