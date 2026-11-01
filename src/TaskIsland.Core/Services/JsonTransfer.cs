using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TaskIsland.Core.Models;

namespace TaskIsland.Core.Services;

/// <summary>Raised when an import file breaks a rule. The message is safe to show to the user.</summary>
public sealed class ImportValidationException(string message) : Exception(message);

/// <summary>JSON export and validated import (PLAN.md section 4.4).</summary>
public static partial class JsonTransfer
{
    public const string AppName = "TaskIsland";
    public const int FormatVersion = 1;
    public const int MaxTasks = 10_000;
    public const int MaxFileBytes = 10 * 1024 * 1024;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorRegex();

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    /// <summary>Serialises the lists and tasks. <paramref name="onlyListId"/> exports a single list.</summary>
    public static string Export(IEnumerable<TodoList> lists, IEnumerable<TaskItem> tasks, DateTime utcNow,
        long? onlyListId = null)
    {
        var listArr = lists.Where(l => onlyListId is null || l.Id == onlyListId).OrderBy(l => l.SortOrder).ToList();
        var names = listArr.ToDictionary(l => l.Id, l => l.Name);
        var taskArr = tasks.Where(t => t.DeletedAt is null && names.ContainsKey(t.ListId))
            .OrderBy(t => t.ListId).ThenBy(t => t.Priority).ThenBy(t => t.SortOrder).ThenBy(t => t.Id).ToList();

        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, WriterOptions))
        {
            w.WriteStartObject();
            w.WriteString("app", AppName);
            w.WriteNumber("formatVersion", FormatVersion);
            w.WriteString("exportedAt", IsoDate.ToUtcString(utcNow));

            w.WriteStartArray("lists");
            foreach (var l in listArr)
            {
                w.WriteStartObject();
                w.WriteString("name", l.Name);
                w.WriteString("color", l.ColorHex);
                w.WriteNumber("sortOrder", l.SortOrder);
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteStartArray("tasks");
            foreach (var t in taskArr)
            {
                w.WriteStartObject();
                w.WriteString("list", names[t.ListId]);
                w.WriteString("title", t.Title);
                if (t.Description is null) w.WriteNull("description"); else w.WriteString("description", t.Description);
                w.WriteString("priority", t.Priority.ToString());
                w.WriteBoolean("isCompleted", t.IsCompleted);
                w.WriteString("createdAt", IsoDate.ToUtcString(t.CreatedAt));
                w.WriteString("updatedAt", IsoDate.ToUtcString(t.UpdatedAt));
                if (t.CompletedAt is { } c) w.WriteString("completedAt", IsoDate.ToUtcString(c)); else w.WriteNull("completedAt");
                if (t.DueDate is { } d) w.WriteString("dueDate", IsoDate.ToLocalString(d, t.DueHasTime)); else w.WriteNull("dueDate");
                w.WriteBoolean("dueHasTime", t.DueHasTime);
                w.WriteNumber("sortOrder", t.SortOrder);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Parses and fully validates an export file. Throws <see cref="ImportValidationException"/> on the first broken
    /// rule, so a bad file is never partly imported. <paramref name="utcNow"/> fills in missing timestamps.
    /// </summary>
    public static TransferData Parse(string json, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ImportValidationException("The file is empty.");
        if (Encoding.UTF8.GetByteCount(json) > MaxFileBytes)
            throw new ImportValidationException("The file is larger than 10 MB.");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { throw new ImportValidationException("The file is not valid JSON."); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new ImportValidationException("The file is not a TaskIsland export.");
            if (!TryString(root, "app", out var app) || app != AppName)
                throw new ImportValidationException("The file is not a TaskIsland export (\"app\" must be \"TaskIsland\").");
            if (!root.TryGetProperty("formatVersion", out var fv) || fv.ValueKind != JsonValueKind.Number
                || !fv.TryGetInt32(out var version) || version != FormatVersion)
                throw new ImportValidationException($"Unsupported export format (formatVersion must be {FormatVersion}).");

            // lists
            var lists = new List<TransferList>();
            var listNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("lists", out var listsEl) && listsEl.ValueKind != JsonValueKind.Null)
            {
                if (listsEl.ValueKind != JsonValueKind.Array) throw new ImportValidationException("\"lists\" must be an array.");
                int i = 0;
                foreach (var el in listsEl.EnumerateArray())
                {
                    i++;
                    if (el.ValueKind != JsonValueKind.Object) throw new ImportValidationException($"List {i} is not an object.");
                    var name = (TryString(el, "name", out var n) ? n : "").Trim();
                    if (name.Length is < 1 or > TodoList.MaxNameLength)
                        throw new ImportValidationException($"List {i}: the name must be 1 to {TodoList.MaxNameLength} characters.");
                    if (!listNames.Add(name))
                        throw new ImportValidationException($"List name \"{name}\" appears more than once.");
                    var color = TryString(el, "color", out var c) ? c : TodoList.DefaultColor;
                    if (!ColorRegex().IsMatch(color))
                        throw new ImportValidationException($"List \"{name}\": the colour must look like #RRGGBB.");
                    lists.Add(new TransferList(name, color.ToUpperInvariant(), ReadInt(el, "sortOrder", lists.Count, $"List \"{name}\"")));
                }
                if (lists.Count > TodoList.MaxLists)
                    throw new ImportValidationException($"The file has more than {TodoList.MaxLists} lists.");
            }

            // tasks
            if (!root.TryGetProperty("tasks", out var tasksEl) || tasksEl.ValueKind != JsonValueKind.Array)
                throw new ImportValidationException("\"tasks\" must be an array.");
            if (tasksEl.GetArrayLength() > MaxTasks)
                throw new ImportValidationException($"The file has more than {MaxTasks:N0} tasks.");

            var tasks = new List<TransferTask>();
            int n2 = 0;
            foreach (var el in tasksEl.EnumerateArray())
            {
                n2++;
                string where = $"Task {n2}";
                if (el.ValueKind != JsonValueKind.Object) throw new ImportValidationException($"{where} is not an object.");

                var title = (TryString(el, "title", out var t) ? t : "").Trim();
                if (title.Length is < 1 or > TaskItem.MaxTitleLength)
                    throw new ImportValidationException($"{where}: the title must be 1 to {TaskItem.MaxTitleLength} characters.");
                where = $"Task \"{Shorten(title)}\"";

                string? listName = null;
                if (TryString(el, "list", out var ln) && !string.IsNullOrWhiteSpace(ln))
                {
                    listName = ln.Trim();
                    if (!listNames.Contains(listName))
                        throw new ImportValidationException($"{where}: the list \"{listName}\" is not defined in \"lists\".");
                }

                var priority = Priority.Medium;
                if (TryString(el, "priority", out var p))
                {
                    if (!Enum.TryParse(p, ignoreCase: true, out priority) || !Enum.IsDefined(priority)
                        || int.TryParse(p, out _))
                        throw new ImportValidationException($"{where}: the priority must be High, Medium or Low.");
                }

                string? description = TryString(el, "description", out var desc) ? desc : null;
                if (description is { Length: > TaskItem.MaxDescriptionLength })
                    throw new ImportValidationException($"{where}: the notes are longer than {TaskItem.MaxDescriptionLength} characters.");

                bool completed = ReadBool(el, "isCompleted", false, where);
                var created = ReadUtc(el, "createdAt", utcNow, where);
                var updated = ReadUtc(el, "updatedAt", created, where);
                DateTime? completedAt = ReadUtcOrNull(el, "completedAt", where);
                if (completed && completedAt is null) completedAt = updated;
                if (!completed) completedAt = null;

                DateTime? due = null;
                if (TryString(el, "dueDate", out var dueText) && !string.IsNullOrWhiteSpace(dueText))
                {
                    if (!IsoDate.TryParseLocal(dueText, out var dv))
                        throw new ImportValidationException($"{where}: the due date must look like 2026-11-30 or 2026-11-30T14:30:00.");
                    due = dv;
                }
                bool hasTime = ReadBool(el, "dueHasTime", due is { TimeOfDay.Ticks: > 0 }, where);
                if (due is null) hasTime = false;

                tasks.Add(new TransferTask(listName, title, description, priority, completed, created, updated,
                    completedAt, due, hasTime, ReadInt(el, "sortOrder", n2, where)));
            }

            return new TransferData(lists, tasks);
        }
    }

    private static string Shorten(string s) => s.Length <= 30 ? s : s[..30] + "...";

    private static bool TryString(JsonElement obj, string name, out string value)
    {
        value = "";
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String) return false;
        value = el.GetString() ?? "";
        return true;
    }

    private static int ReadInt(JsonElement obj, string name, int fallback, string where)
    {
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind == JsonValueKind.Null) return fallback;
        if (el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new ImportValidationException($"{where}: \"{name}\" must be a whole number.");
        return v;
    }

    private static bool ReadBool(JsonElement obj, string name, bool fallback, string where)
    {
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind == JsonValueKind.Null) return fallback;
        if (el.ValueKind is JsonValueKind.True) return true;
        if (el.ValueKind is JsonValueKind.False) return false;
        throw new ImportValidationException($"{where}: \"{name}\" must be true or false.");
    }

    private static DateTime ReadUtc(JsonElement obj, string name, DateTime fallback, string where) =>
        ReadUtcOrNull(obj, name, where) ?? fallback;

    private static DateTime? ReadUtcOrNull(JsonElement obj, string name, string where)
    {
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind == JsonValueKind.Null) return null;
        if (el.ValueKind != JsonValueKind.String || !IsoDate.TryParseUtc(el.GetString(), out var v))
            throw new ImportValidationException($"{where}: \"{name}\" is not a valid date.");
        return v;
    }
}
