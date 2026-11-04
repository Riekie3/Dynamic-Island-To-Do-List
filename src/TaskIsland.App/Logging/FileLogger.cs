using System.IO;
using System.Text;

namespace TaskIsland.App.Logging;

/// <summary>
/// Minimal file log: <c>Logs\taskisland-yyyyMMdd.log</c>, one line per event (PLAN.md section 7.10). Appends only when
/// something is logged, so an idle app writes nothing. Files older than 14 days are removed at start. Phase 4 hooks the
/// global exception handlers into it.
/// </summary>
public static class FileLogger
{
    private static readonly object Gate = new();
    private static string? _dir;

    public static void Init(string logsDirectory)
    {
        lock (Gate)
        {
            _dir = logsDirectory;
            try
            {
                Directory.CreateDirectory(logsDirectory);
                foreach (var f in Directory.GetFiles(logsDirectory, "taskisland-*.log"))
                    if (File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-14)) File.Delete(f);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message) => Write("WARN", message, null);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        lock (Gate)
        {
            if (_dir is null) return;
            try
            {
                var sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(' ').Append(level).Append(' ').Append(message);
                if (ex is not null) sb.Append(" | ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
                sb.AppendLine();
                File.AppendAllText(Path.Combine(_dir, $"taskisland-{DateTime.Now:yyyyMMdd}.log"), sb.ToString());
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
