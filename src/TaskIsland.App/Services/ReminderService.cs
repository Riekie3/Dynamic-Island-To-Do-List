using System.Windows.Threading;
using TaskIsland.App.Logging;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;

namespace TaskIsland.App.Services;

/// <summary>
/// Due-date reminders (PLAN.md section 7.9). One one-shot <see cref="DispatcherTimer"/> set to the next due moment (at most
/// 24 h ahead), recomputed after every task change, wake and clock change. Never polls. Each reminder fires once: it is
/// marked in the database (<c>ReminderShownAt</c>) and remembered here with its due moment, so a changed due date can
/// remind again.
/// </summary>
public sealed class ReminderService : IDisposable
{
    private enum Check { Startup, Timer, DataChanged, Wake }

    private static readonly TimeSpan Slack = TimeSpan.FromMilliseconds(250);
    private readonly ITaskRepository _repo;
    private readonly AppSettings _settings;
    private readonly IClock _clock;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<long, DateTime> _notified = [];
    private IReadOnlyList<TaskItem> _tasks = [];
    private bool _started;

    public ReminderService(ITaskRepository repo, AppSettings settings, IClock clock)
    {
        _repo = repo;
        _settings = settings;
        _clock = clock;
        _timer = new DispatcherTimer(DispatcherPriority.Normal);
        _timer.Tick += (_, _) => { _timer.Stop(); Run(Check.Timer); };
    }

    /// <summary>A reminder (or a batch of them) is due: the App shows the balloon, pulses the island and the Surprised face.</summary>
    public event Action<ReminderNotice>? Notify;

    /// <summary>Resolves list names for the balloon heading.</summary>
    public Func<long, string?> ListName { get; set; } = _ => null;

    /// <summary>When the timer will fire next (null = not armed). For tests and the log.</summary>
    public DateTime? NextCheck { get; private set; }

    /// <summary>Start-up: tasks that came due while TaskIsland was not running are shown as one combined balloon.</summary>
    public void Start(IReadOnlyList<TaskItem> tasks)
    {
        _tasks = tasks;
        _started = true;
        Run(Check.Startup);
    }

    /// <summary>The view model reloaded (any add, edit, tick, delete, import).</summary>
    public void OnDataChanged(IReadOnlyList<TaskItem> tasks)
    {
        _tasks = tasks;
        if (_started) Run(Check.DataChanged);
    }

    /// <summary>Wake, unlock, clock change or the Reminders setting changed.</summary>
    public void Recompute()
    {
        if (_started) Run(Check.Wake);
    }

    private void Run(Check kind)
    {
        _timer.Stop();
        NextCheck = null;
        if (!_settings.RemindersEnabled) return;
        var now = _clock.Now;
        var pending = ReminderScheduler.GetOverdue(_tasks, now).Where(r => !AlreadyNotified(r)).ToList();

        if (kind == Check.DataChanged && pending.Count > 0)
        {
            // A due date the user just set in the past is not "due now": the row already shows it in red. Mark it quietly.
            var quiet = pending.Where(r => ReminderText.IsLate(r, now)).ToList();
            if (quiet.Count > 0)
            {
                Mark(quiet);
                FileLogger.Info($"reminders: {quiet.Count} already past when saved, marked without a balloon");
                pending = pending.Except(quiet).ToList();
            }
        }

        if (pending.Count > 0)
        {
            bool late = pending.All(r => ReminderText.IsLate(r, now));
            Mark(pending);
            if (ReminderText.Describe(pending, late, ListName) is { } notice)
            {
                FileLogger.Info($"reminder ({kind}): {pending.Count} task(s), \"{notice.Text}\"");
                Notify?.Invoke(notice);
            }
        }

        var next = ReminderScheduler.GetNextUpcoming(_tasks.Where(t => !_notified.ContainsKey(t.Id)
            || ReminderScheduler.GetDueMoment(t) != _notified[t.Id]), now);
        if (next is null) return;
        _timer.Interval = ReminderText.Wait(next.Moment, now) + Slack;
        _timer.Start();
        NextCheck = now + _timer.Interval;
    }

    private bool AlreadyNotified(DueReminder r) => _notified.TryGetValue(r.Task.Id, out var m) && m == r.Moment;

    private void Mark(IEnumerable<DueReminder> reminders)
    {
        var ids = new List<long>();
        foreach (var r in reminders)
        {
            _notified[r.Task.Id] = r.Moment;
            ids.Add(r.Task.Id);
        }
        _ = SaveAsync(ids);
    }

    private async Task SaveAsync(List<long> ids)
    {
        foreach (var id in ids)
        {
            try { await _repo.MarkReminderShownAsync(id); }
            catch (Exception ex) { FileLogger.Error($"reminders: could not mark task {id} as reminded", ex); }
        }
    }

    public void Dispose() => _timer.Stop();
}
