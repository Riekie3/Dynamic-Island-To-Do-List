using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TaskIsland.App.Logging;
using TaskIsland.App.Views;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using TaskIsland.Data;

namespace TaskIsland.App.ViewModels;

/// <summary>Data page (export, import, backup, restore, clear completed) and About page (PLAN.md section 6.6).</summary>
public sealed partial class SettingsViewModel
{
    public const string SiteUrl = "https://rie-sources.com";
    public const string GitHubUrl = "https://github.com/Riekie3/Dynamic-Island-To-Do-List";

    [ObservableProperty] private string _dataStatus = "";
    [ObservableProperty] private bool _dataStatusIsError;
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string _lastBackupText = "";

    public bool DailyBackupEnabled
    {
        get => _s.DailyBackupEnabled;
        set => Set(_s.DailyBackupEnabled, value, v => _s.DailyBackupEnabled = v, AppSettings.Keys.DailyBackupEnabled);
    }

    private void UpdateBackupText()
    {
        LastBackupText = _s.LastBackupAt is { } t
            ? $"Last automatic backup: {t.ToLocalTime():yyyy-MM-dd HH:mm}. The newest 7 are kept."
            : "No automatic backup yet. The newest 7 are kept.";
    }

    private void Status(string text, bool error = false)
    {
        DataStatus = text;
        DataStatusIsError = error;
    }

    /// <summary>Save dialogs start in Downloads: Controlled folder access protects Documents and Desktop (PLAN.md section 7.11).</summary>
    private static string DownloadsFolder()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        return Directory.Exists(path) ? path : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static string WriteFailure(Exception ex) =>
        ex is UnauthorizedAccessException or IOException
            ? "Windows blocked saving here (Controlled folder access may be on). Choose another folder, such as Downloads."
            : $"That didn't work: {ex.Message}";

    // ---- export ----

    [RelayCommand]
    private async Task ExportJsonAsync()
    {
        Status("");
        long? only = null;
        if (Island.SelectedListId != 0 && Island.ListName(Island.SelectedListId) is { } current)
        {
            int pick = DialogWindow.Choose(Owner, "Export JSON", "Which lists do you want to export?",
                new DialogButton("All lists", DialogButtonKind.Primary), new DialogButton($"Only \"{current}\""));
            if (pick < 0) return;
            if (pick == 1) only = Island.SelectedListId;
        }
        var dialog = new SaveFileDialog
        {
            Title = "Export tasks",
            Filter = "TaskIsland export (*.json)|*.json",
            FileName = $"taskisland-export-{_clock.Now:yyyyMMdd}.json",
            InitialDirectory = DownloadsFolder(),
            AddExtension = true,
            DefaultExt = ".json",
        };
        if (dialog.ShowDialog(Owner) != true) return;
        try
        {
            Busy = true;
            var json = JsonTransfer.Export(await _listsRepo.GetAllAsync(), await _tasks.GetAllAsync(), _clock.UtcNow, only);
            await File.WriteAllTextAsync(dialog.FileName, json, new System.Text.UTF8Encoding(false));
            FileLogger.Info($"exported {(only is null ? "all lists" : "one list")} to {Path.GetFileName(dialog.FileName)}");
            Status($"Exported to {dialog.FileName}");
        }
        catch (Exception ex)
        {
            FileLogger.Error("export failed", ex);
            Status(WriteFailure(ex), error: true);
        }
        finally
        {
            Busy = false;
        }
    }

    // ---- import ----

    [RelayCommand]
    private async Task ImportJsonAsync()
    {
        Status("");
        var dialog = new OpenFileDialog
        {
            Title = "Import tasks",
            Filter = "TaskIsland export (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = DownloadsFolder(),
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(Owner) != true) return;

        TransferData data;
        try
        {
            if (new FileInfo(dialog.FileName).Length > JsonTransfer.MaxFileBytes)
                throw new ImportValidationException("That file is larger than 10 MB, so it is not a TaskIsland export.");
            data = JsonTransfer.Parse(await File.ReadAllTextAsync(dialog.FileName), _clock.UtcNow);
        }
        catch (ImportValidationException ex)
        {
            Status("Nothing was imported. " + ex.Message, error: true);
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            FileLogger.Error("import: could not read the file", ex);
            Status("Nothing was imported. The file could not be read.", error: true);
            return;
        }

        string what = $"{data.Tasks.Count} {(data.Tasks.Count == 1 ? "task" : "tasks")} in {data.Lists.Count} {(data.Lists.Count == 1 ? "list" : "lists")}";
        int mode = DialogWindow.Choose(Owner, "Import tasks", $"The file has {what}.\n\nMerge adds what is missing and skips tasks you already have. " +
            "Replace swaps all your lists and tasks for the file's.",
            new DialogButton("Merge", DialogButtonKind.Primary), new DialogButton("Replace everything", DialogButtonKind.Danger));
        if (mode < 0) return;

        bool backedUp = false;
        try
        {
            Busy = true;
            if (mode == 1)
            {
                if (!DialogWindow.Confirm(Owner, "Replace everything?",
                        "All current lists and tasks will be replaced by the file's. A backup of your current data is saved first.",
                        "Replace", destructive: true)) return;
            }
            // A merge adds items one by one, so a failure part-way can leave some of them behind: keep a copy first.
            var safety = await _backup.BackupAsync(prefix: "pre-import-");
            FileLogger.Info($"backup before import ({(mode == 1 ? "replace" : "merge")}): {safety}");
            backedUp = true;
            var summary = await _import.ImportAsync(data, mode == 1 ? ImportMode.Replace : ImportMode.Merge);
            await Island.RefreshAsync(reloadSettings: true);
            RefreshAll();
            string text = mode == 1
                ? $"Replaced everything: {summary.TasksImported} tasks in {summary.ListsCreated} lists."
                : $"Imported {summary.TasksImported}, skipped {summary.DuplicatesSkipped} duplicates" +
                  (summary.ListsCreated > 0 ? $", added {summary.ListsCreated} new {(summary.ListsCreated == 1 ? "list" : "lists")}." : ".");
            FileLogger.Info("import: " + text);
            Status(text);
        }
        catch (Exception ex)
        {
            FileLogger.Error("import failed", ex);
            await TryRefreshAsync();
            Status(ex is InvalidOperationException ? ex.Message
                : backedUp ? "The import did not finish, so some items may have been added. Your data from before the import is saved as a \"pre-import\" file in the Backups folder."
                : "The import failed and nothing was changed. See the log for details.", error: true);
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task TryRefreshAsync()
    {
        try { await Island.RefreshAsync(reloadSettings: true); RefreshAll(); }
        catch (Exception ex) { FileLogger.Error("reload after a failed data action also failed", ex); }
    }

    // ---- backup / restore ----

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        Status("");
        var dialog = new SaveFileDialog
        {
            Title = "Back up now",
            Filter = "TaskIsland backup (*.db)|*.db",
            FileName = $"taskisland-backup-{_clock.Now:yyyyMMdd-HHmm}.db",
            InitialDirectory = DownloadsFolder(),
            AddExtension = true,
            DefaultExt = ".db",
        };
        if (dialog.ShowDialog(Owner) != true) return;
        try
        {
            Busy = true;
            var path = await _backup.BackupAsync(dialog.FileName);
            FileLogger.Info($"manual backup written: {Path.GetFileName(path)}");
            Status($"Backup saved to {path}");
        }
        catch (Exception ex)
        {
            FileLogger.Error("manual backup failed", ex);
            Status(WriteFailure(ex), error: true);
        }
        finally
        {
            Busy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        Status("");
        var dialog = new OpenFileDialog
        {
            Title = "Restore from backup",
            Filter = "TaskIsland backup (*.db)|*.db",
            InitialDirectory = Directory.Exists(_paths.BackupsDir) ? _paths.BackupsDir : DownloadsFolder(),
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(Owner) != true) return;
        try
        {
            Busy = true;
            try { await BackupService.ValidateBackupAsync(dialog.FileName); }
            catch (InvalidDataException ex)
            {
                Status("Nothing was restored. " + ex.Message, error: true);
                return;
            }
            if (!DialogWindow.Confirm(Owner, "Restore this backup?",
                    "Your current lists, tasks and settings will be replaced by the backup. The current data is saved first as a \"pre-restore\" file in the Backups folder.",
                    "Restore", destructive: true)) return;
            var safety = await _backup.RestoreAsync(dialog.FileName);
            FileLogger.Info($"restored {Path.GetFileName(dialog.FileName)}; previous data kept as {Path.GetFileName(safety)}");
            _s.CopyFrom(await _store.LoadAsync());
            _apply.ApplyAll();
            await Island.RefreshAsync(reloadSettings: true);
            RefreshAll();
            Status($"Restored. Your previous data was kept as {Path.GetFileName(safety)} in the Backups folder.");
        }
        catch (InvalidDataException ex)
        {
            Status("Nothing was restored. " + ex.Message, error: true);
        }
        catch (Exception ex)
        {
            FileLogger.Error("restore failed", ex);
            await TryRefreshAsync();
            Status("The restore failed. See the log for details.", error: true);
        }
        finally
        {
            Busy = false;
        }
    }

    [RelayCommand]
    private void OpenDataFolder() => App.OpenFolder(_paths.Root);

    [RelayCommand]
    private async Task ClearCompletedAsync()
    {
        Status("");
        int count = Island.CompletedCount;
        if (count == 0)
        {
            Status("There are no completed tasks to clear.");
            return;
        }
        if (!DialogWindow.Confirm(Owner, "Clear completed tasks?",
                $"{count} completed {(count == 1 ? "task" : "tasks")} in all lists will be removed. You can undo this for 5 seconds.",
                "Clear", destructive: true)) return;
        int cleared = await Island.ClearAllCompletedAsync();
        FileLogger.Info($"cleared {cleared} completed task(s) from Settings");
        RefreshLists();
        Status(cleared == 0 ? "Nothing was cleared." : $"Cleared {cleared} completed {(cleared == 1 ? "task" : "tasks")}.");
    }

    // ---- About ----

    public string AppVersion { get; } =
        (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    public string BuildDate { get; } = BuildDateText();

    public string RuntimeText { get; } = RuntimeInformation.FrameworkDescription;

    public string DataFolder => _paths.Root;

    private static string BuildDateText()
    {
        try
        {
            var path = Environment.ProcessPath ?? Assembly.GetEntryAssembly()?.Location;
            return path is not null ? File.GetLastWriteTime(path).ToString("yyyy-MM-dd") : "unknown";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "unknown";
        }
    }

    /// <summary>The two About links open the default browser, only on a click (PLAN.md section 7.11 rule 3).</summary>
    [RelayCommand]
    private void OpenLink(string? url)
    {
        if (url is not (SiteUrl or GitHubUrl)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { FileLogger.Warn($"could not open {url}: {ex.Message}"); }
    }
}
