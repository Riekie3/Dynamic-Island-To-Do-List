namespace TaskIsland.Data;

/// <summary>Where TaskIsland keeps its files: %LocalAppData%\TaskIsland\{Data,Backups,Logs}. Tests pass their own root.</summary>
public sealed class AppPaths
{
    public AppPaths() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskIsland"))
    {
    }

    public AppPaths(string root)
    {
        Root = root;
        DataDir = Path.Combine(root, "Data");
        BackupsDir = Path.Combine(root, "Backups");
        LogsDir = Path.Combine(root, "Logs");
        DatabasePath = Path.Combine(DataDir, "taskisland.db");
    }

    public string Root { get; }
    public string DataDir { get; }
    public string BackupsDir { get; }
    public string LogsDir { get; }
    public string DatabasePath { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(BackupsDir);
        Directory.CreateDirectory(LogsDir);
    }
}
