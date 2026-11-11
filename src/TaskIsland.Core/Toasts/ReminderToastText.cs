using TaskIsland.Core.Services;

namespace TaskIsland.Core.Toasts;

/// <summary>What a reminder toast shows: the tab title, the body text, and which of the toast looks it uses.</summary>
public sealed record ReminderToastContent(string Tab, string? Body, ToastState State);

/// <summary>
/// Turns a <see cref="ReminderNotice"/> into the Sileo-style reminder toast (SILEO-PLAN.md section 4): the tab says what
/// is going on ("Due now", "Overdue", "In 5 minutes"), the body says which task and from which list.
/// Pure and unit-tested; the notice texts it reads are written by <see cref="ReminderText"/>.
/// </summary>
public static class ReminderToastText
{
    private const string Bell = "⏰ ";

    public static ReminderToastContent From(ReminderNotice notice)
    {
        string text = notice.Text.StartsWith(Bell, StringComparison.Ordinal) ? notice.Text[Bell.Length..] : notice.Text;

        // One task: "Due now: X", "Overdue: X" or "In 5 minutes: X".
        int colon = text.IndexOf(": ", StringComparison.Ordinal);
        if (colon > 0)
        {
            string tab = text[..colon];
            string task = text[(colon + 2)..];
            var state = tab.StartsWith("Overdue", StringComparison.OrdinalIgnoreCase) ? ToastState.Warning : ToastState.Info;
            return new ReminderToastContent(tab, WithList(task, notice), state);
        }

        // Several tasks: "3 tasks due now", "2 tasks overdue", "2 tasks due in 10 minutes".
        bool overdue = text.Contains("overdue", StringComparison.OrdinalIgnoreCase);
        return new ReminderToastContent(Capitalise(text), notice.ListId is null ? "Open TaskIsland to see them." : WithList("Open the list to see them.", notice),
            overdue ? ToastState.Warning : ToastState.Info);
    }

    /// <summary>"Task title" plus the list name on a second line, when the notice came from a named list.</summary>
    private static string WithList(string task, ReminderNotice notice) =>
        notice.Title is { Length: > 0 } list && list != "TaskIsland" ? $"{task}\n{list}" : task;

    private static string Capitalise(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
