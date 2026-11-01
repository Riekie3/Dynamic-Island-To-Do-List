namespace TaskIsland.Data;

/// <summary>
/// Ordered schema scripts, applied when PRAGMA user_version is lower. Never edit an existing script: add a new one,
/// so existing user data survives upgrades (PLAN.md section 4.1).
/// </summary>
internal static class Migrations
{
    public static readonly IReadOnlyList<(int Version, string Sql)> All =
    [
        (1, """
            CREATE TABLE Lists (
              Id         INTEGER PRIMARY KEY AUTOINCREMENT,
              Name       TEXT    NOT NULL CHECK (length(Name) BETWEEN 1 AND 40),
              ColorHex   TEXT    NOT NULL DEFAULT '#1E90FF',
              SortOrder  INTEGER NOT NULL DEFAULT 0,
              CreatedAt  TEXT    NOT NULL,
              UpdatedAt  TEXT    NOT NULL
            );
            CREATE UNIQUE INDEX UX_Lists_Name ON Lists (Name COLLATE NOCASE);

            CREATE TABLE Tasks (
              Id              INTEGER PRIMARY KEY AUTOINCREMENT,
              ListId          INTEGER NOT NULL REFERENCES Lists(Id) ON DELETE CASCADE,
              Title           TEXT    NOT NULL CHECK (length(Title) BETWEEN 1 AND 500),
              Description     TEXT,
              Priority        INTEGER NOT NULL DEFAULT 1 CHECK (Priority IN (0,1,2)),
              IsCompleted     INTEGER NOT NULL DEFAULT 0 CHECK (IsCompleted IN (0,1)),
              CreatedAt       TEXT    NOT NULL,
              UpdatedAt       TEXT    NOT NULL,
              CompletedAt     TEXT,
              DueDate         TEXT,
              DueHasTime      INTEGER NOT NULL DEFAULT 0,
              ReminderShownAt TEXT,
              SortOrder       INTEGER NOT NULL DEFAULT 0,
              DeletedAt       TEXT
            );
            CREATE INDEX IX_Tasks_Active ON Tasks (ListId, DeletedAt, IsCompleted, Priority, SortOrder);

            CREATE TABLE Settings (
              SettingKey   TEXT PRIMARY KEY,
              SettingValue TEXT NOT NULL
            );

            INSERT INTO Lists (Name, ColorHex, SortOrder, CreatedAt, UpdatedAt)
              VALUES ('Personal',    '#1E90FF', 0, strftime('%Y-%m-%dT%H:%M:%fZ','now'), strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            INSERT INTO Lists (Name, ColorHex, SortOrder, CreatedAt, UpdatedAt)
              VALUES ('Office Work', '#A970FF', 1, strftime('%Y-%m-%dT%H:%M:%fZ','now'), strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            INSERT INTO Settings (SettingKey, SettingValue)
              SELECT 'DefaultListId', CAST(Id AS TEXT) FROM Lists WHERE Name = 'Personal';
            """),
    ];

    public static int Latest => All[^1].Version;
}
