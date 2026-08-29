using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;

namespace AuthenticatorChooser.Autostart;

public interface IAutostartService {

    bool IsRegistered();

    bool Register(string executablePath, string? arguments);

    bool Unregister();

}

public sealed class ScheduledTaskAutostartService: IAutostartService {

    public const string TaskFolderNamePrefix = nameof(AuthenticatorChooser);

    private readonly string domainAndUsername;
    private readonly string taskName;

    public ScheduledTaskAutostartService(string domainAndUsername, string userName) {
        this.domainAndUsername = domainAndUsername;
        taskName = $"{TaskFolderNamePrefix} \u2013 {userName}";
    }

    public static string TaskNameFor(string userName) => $"{TaskFolderNamePrefix} \u2013 {userName}";

    public bool IsRegistered() {
        using TaskService service = new();
        return service.RootFolder.Tasks.Any(task => string.Equals(task.Name, taskName, StringComparison.Ordinal));
    }

    public bool Register(string executablePath, string? arguments) {
        try {
            TaskDefinition scheduledTask = TaskService.Instance.NewTask();
            scheduledTask.RegistrationInfo.Author = "AryaPaw (fork of Ben Hutchison)";
            scheduledTask.RegistrationInfo.Date   = DateTime.Now;
            scheduledTask.RegistrationInfo.Description =
                $"{TaskFolderNamePrefix} skips the phone pairing option and chooses the USB security key in Windows FIDO/WebAuthn prompts.\n\nThis scheduled task starts {TaskFolderNamePrefix} on login with elevated permissions required to interact with Windows 11 FIDO prompts.\n\nhttps://github.com/AryaPaw/{TaskFolderNamePrefix}";
            scheduledTask.Principal.RunLevel                  = TaskRunLevel.Highest;
            scheduledTask.Settings.Enabled                    = true;
            scheduledTask.Settings.ExecutionTimeLimit         = TimeSpan.Zero;
            scheduledTask.Settings.DisallowStartIfOnBatteries = false;
            scheduledTask.Settings.StopIfGoingOnBatteries     = false;
            scheduledTask.Settings.Compatibility              = TaskCompatibility.V2_3;
            scheduledTask.Actions.Add(executablePath, arguments);
            scheduledTask.Triggers.Add(new LogonTrigger { Enabled = true, UserId = domainAndUsername, Delay = TimeSpan.FromSeconds(15) });
            TaskService.Instance.RootFolder.RegisterTaskDefinition(taskName, scheduledTask, TaskCreation.CreateOrUpdate, domainAndUsername, null,
                TaskLogonType.InteractiveToken);

            using RegistryKey? userRun = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (userRun is not null) {
                try {
                    userRun.DeleteValue(TaskFolderNamePrefix, true);
                } catch (ArgumentException) {
                    // old registry startup entry already removed
                }
            }

            return true;
        } catch (Exception e) when (e is not OutOfMemoryException) {
            return false;
        }
    }

    public bool Unregister() {
        try {
            using TaskService service = new();
            if (service.RootFolder.Tasks.Any(task => string.Equals(task.Name, taskName, StringComparison.Ordinal))) {
                service.RootFolder.DeleteTask(taskName, false);
            }

            return true;
        } catch (Exception e) when (e is not OutOfMemoryException) {
            return false;
        }
    }

}
