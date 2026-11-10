using Microsoft.Data.Sqlite;

namespace TaskIsland.Data;

/// <summary>
/// Destructive maintenance requested from Settings > Data: clear the Backups folder, or return the database to the state
/// of a brand-new install (starter lists, default settings, no tasks and no backups).
/// </summary>
public sealed class DataMaintenanceService(Database db, BackupService backup)
{
    // The same starter rows that migration 1 writes into a new database. Keep the two in step.
    private const string StarterData = """
        INSERT INTO Lists (Name, ColorHex, SortOrder, CreatedAt, UpdatedAt)
          VALUES ('Personal',    '#1E90FF', 0, strftime('%Y-%m-%dT%H:%M:%fZ','now'), strftime('%Y-%m-%dT%H:%M:%fZ','now'));
        INSERT INTO Lists (Name, ColorHex, SortOrder, CreatedAt, UpdatedAt)
          VALUES ('Office Work', '#A970FF', 1, strftime('%Y-%m-%dT%H:%M:%fZ','now'), strftime('%Y-%m-%dT%H:%M:%fZ','now'));
        INSERT INTO Settings (SettingKey, SettingValue)
          SELECT 'DefaultListId', CAST(Id AS TEXT) FROM Lists WHERE Name = 'Personal';
        """;

    /// <summary>
    /// Clears every list, task and setting, puts the starter lists back and resets the id counters, then deletes every
    /// backup. The schema is kept, so the running app can reload immediately. Returns the number of backup files removed.
    /// </summary>
    public async Task<int> ResetToFirstRunAsync()
    {
        await using (var conn = await db.OpenAsync())
        await using (var tx = await conn.BeginAsync())
        {
            await conn.ExecAsync(tx, """
                DELETE FROM Tasks;
                DELETE FROM Lists;
                DELETE FROM Settings;
                DELETE FROM sqlite_sequence WHERE name IN ('Tasks', 'Lists');
                """ + StarterData);
            await tx.CommitAsync();
        }
        return backup.DeleteAllBackups();
    }
}
