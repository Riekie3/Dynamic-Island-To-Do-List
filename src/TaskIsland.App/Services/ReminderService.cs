using System.Windows.Threading;
using TaskIsland.App.Logging;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;

namespace TaskIsland.App.Services;

/// <summary>
/// Due-date reminders (PLAN.md section 7.9). One one-shot <see cref="DispatcherTimer"/> set to the next due moment (at most
/// 24 h ahead), recomputed after every task change, wake and clock change. Never polls. Each reminder fires once: it is
/// marked in the database (<c>ReminderShownAt</c>) and remembered here with its due moment, so a changed due date can
/// remind again. With <c>ReminderLeadMinutes</c> set, tasks with a due time also get one early reminder
/// (<c>EarlyReminderShownAt</c>); an early moment found already past (start-up, wake, a task saved inside the window) is
/// marked without a notification.
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
    private readonly Dictionary<long, DateTime> _earlyNotified = [];
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

    /// <summary>Wake, unlock, clock change or a reminder setting (on/off, lead) changed.</summary>
    public void Recompute()
    {
        if (_started) Run(Check.Wake);
    }

    /// <summary>The tasks as the scheduler should see them: reminders already shown in this run count as shown, even before the next reload.</summary>
    private List<TaskItem> View()
    {
        int lead = _settings.ReminderLeadMinutes;
        var view = new List<TaskItem>(_tasks.Count);
        foreach (var t in _tasks)
        {
            var due = ReminderScheduler.GetDueMoment(t);
            bool dueDone = due is not null && _notified.TryGetValue(t.Id, out var dm) && dm == due;
            bool earlyDone = due is not null && lead > 0 && _earlyNotified.TryGetValue(t.Id, out var em) && em == due;
            if (!dueDone && !earlyDone) { view.Add(t); continue; }
            var c = t.Clone();
            if (dueDone) c.ReminderShownAt ??= DateTime.UtcNow;
            if (earlyDone) c.EarlyReminderShownAt ??= DateTime.UtcNow;
            view.Add(c);
        }
        return view;
    }

    private void Run(Check kind)
    {
        _timer.Stop();
        NextCheck = null;
        if (!_settings.RemindersEnabled) return;
        var now = _clock.Now;
        int lead = _settings.ReminderLeadMinutes;
        var view = View();

        // Early reminders (before the due time). Only the timer firing on time notifies; anything found already past is
        // marked quietly, so a task saved or found inside the window does not announce itself.
        var early = ReminderScheduler.GetEarlyDue(view, now, lead).ToList();
        if (early.Count > 0)
        {
            var quietEarly = kind != Check.Timer ? early : early.Where(r => ReminderText.IsLate(r, now)).ToList();
            if (quietEarly.Count > 0)
            {
                MarkEarly(quietEarly);
                FileLogger.Info($"early reminders ({kind}): {quietEarly.Count} already inside the {lead}-minute window, marked without a notification");
            }
            var fire = early.Except(quietEarly).ToList();
            if (fire.Count > 0)
            {
                MarkEarly(fire);
                if (ReminderText.DescribeEarly(fire, lead, ListName) is { } notice)
                {
                    FileLogger.Info($"early reminder ({kind}): {fire.Count} task(s), \"{notice.Text}\"");
                    Notify?.Invoke(notice);
                }
            }
            view = View();
        }

        var pending = ReminderScheduler.GetOverdue(view, now).Where(r => !AlreadyNotified(r)).ToList();

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

        var next = ReminderScheduler.GetNextUpcoming(View(), now, lead);
        if (next is null) return;
        _timer.Interval = ReminderText.Wait(next.Moment, now) + Slack;
        _timer.Start();
        NextCheck = now + _timer.Interval;
        NextIsEarly = next.Early;
    }

    /// <summary>The armed timer is for an early reminder (tests and the log).</summary>
    public bool NextIsEarly { get; private set; }

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

    private void MarkEarly(IEnumerable<DueReminder> reminders)
    {
        var ids = new List<long>();
        foreach (var r in reminders)
        {
            _earlyNotified[r.Task.Id] = ReminderScheduler.GetDueMoment(r.Task) ?? r.Moment;
            ids.Add(r.Task.Id);
        }
        _ = SaveEarlyAsync(ids);
    }

    private async Task SaveEarlyAsync(List<long> ids)
    {
        foreach (var id in ids)
        {
            try { await _repo.MarkEarlyReminderShownAsync(id); }
            catch (Exception ex) { FileLogger.Error($"reminders: could not mark task {id} as early-reminded", ex); }
        }
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
