using TaskIsland.Core.Models;
using TaskIsland.Core.Services;

namespace TaskIsland.Data;

public sealed class SqliteSettingsStore(Database db) : ISettingsStore
{
    private const string Upsert = """
        INSERT INTO Settings (SettingKey, SettingValue) VALUES (@k, @v)
        ON CONFLICT(SettingKey) DO UPDATE SET SettingValue = excluded.SettingValue
        """;

    public async Task<string?> GetAsync(string key)
    {
        await using var conn = await db.OpenAsync();
        return await conn.ScalarAsync<string>(null, "SELECT SettingValue FROM Settings WHERE SettingKey = @k", ("@k", key));
    }

    public async Task SetAsync(string key, string value)
    {
        await using var conn = await db.OpenAsync();
        await conn.ExecAsync(null, Upsert, ("@k", key), ("@v", value));
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync()
    {
        await using var conn = await db.OpenAsync();
        var map = new Dictionary<string, string>();
        await using var cmd = conn.Cmd(null, "SELECT SettingKey, SettingValue FROM Settings");
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) map[r.GetString(0)] = r.GetString(1);
        return map;
    }

    public async Task<AppSettings> LoadAsync() => AppSettings.FromDictionary(await GetAllAsync());

    public async Task SaveAsync(AppSettings settings)
    {
        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginAsync();
        foreach (var (key, value) in settings.ToDictionary())
            await conn.ExecAsync(tx, Upsert, ("@k", key), ("@v", value));
        // LastBackupAt is optional: clear a stale value when the setting was reset to null.
        if (settings.LastBackupAt is null)
            await conn.ExecAsync(tx, "DELETE FROM Settings WHERE SettingKey = @k", ("@k", AppSettings.Keys.LastBackupAt));
        await tx.CommitAsync();
    }
}
