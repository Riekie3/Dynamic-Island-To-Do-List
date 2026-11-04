namespace TaskIsland.Core.Mascot;

/// <summary>
/// "5 tasks ticked within 30 minutes" earns the Love face (PLAN.md section 6.8). Feed it every tick; it says when the
/// streak is reached and then starts counting again.
/// </summary>
public sealed class TickStreak(int count = 5, TimeSpan? window = null)
{
    private readonly TimeSpan _window = window ?? TimeSpan.FromMinutes(30);
    private readonly List<DateTime> _ticks = [];

    public bool Record(DateTime nowUtc)
    {
        _ticks.RemoveAll(t => nowUtc - t > _window);
        _ticks.Add(nowUtc);
        if (_ticks.Count < count) return false;
        _ticks.Clear();
        return true;
    }

    /// <summary>An untick takes the most recent tick back.</summary>
    public void Undo()
    {
        if (_ticks.Count > 0) _ticks.RemoveAt(_ticks.Count - 1);
    }
}
