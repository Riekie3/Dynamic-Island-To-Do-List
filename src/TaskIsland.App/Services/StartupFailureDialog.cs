namespace TaskIsland.App.Services;

public enum StartupFailureChoice { OpenDataFolder, RestoreFromBackup, Exit }

/// <summary>
/// The database could not be opened at start-up (locked by another program or damaged), PLAN.md section 7.10. A plain
/// Windows task dialog with three choices; nothing is changed until the user picks one.
/// </summary>
public static class StartupFailureDialog
{
    public static StartupFailureChoice Ask(string details)
    {
        var open = new System.Windows.Forms.TaskDialogButton("Open data folder");
        var restore = new System.Windows.Forms.TaskDialogButton("Restore from backup…");
        var exit = new System.Windows.Forms.TaskDialogButton("Exit");
        var page = new System.Windows.Forms.TaskDialogPage
        {
            Caption = "TaskIsland",
            Heading = "TaskIsland can't open your tasks",
            Text = "The task database could not be opened. Another program may be using it, or the file may be damaged. " +
                   "Nothing has been changed or deleted.\n\nYou can look at the data folder, restore a backup (the current " +
                   "file is kept in the Backups folder), or exit and try again later.",
            Expander = new System.Windows.Forms.TaskDialogExpander(details) { CollapsedButtonText = "Details" },
            Icon = System.Windows.Forms.TaskDialogIcon.Error,
            AllowCancel = true,
            Buttons = { open, restore, exit },
            DefaultButton = exit,
        };
        var result = System.Windows.Forms.TaskDialog.ShowDialog(page, System.Windows.Forms.TaskDialogStartupLocation.CenterScreen);
        if (result == open) return StartupFailureChoice.OpenDataFolder;
        if (result == restore) return StartupFailureChoice.RestoreFromBackup;
        return StartupFailureChoice.Exit;
    }

    public static void Info(string heading, string text, bool error)
    {
        var page = new System.Windows.Forms.TaskDialogPage
        {
            Caption = "TaskIsland",
            Heading = heading,
            Text = text,
            Icon = error ? System.Windows.Forms.TaskDialogIcon.Error : System.Windows.Forms.TaskDialogIcon.Information,
            Buttons = { System.Windows.Forms.TaskDialogButton.OK },
        };
        System.Windows.Forms.TaskDialog.ShowDialog(page, System.Windows.Forms.TaskDialogStartupLocation.CenterScreen);
    }
}
