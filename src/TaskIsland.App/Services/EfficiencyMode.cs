using System.Diagnostics;
using System.Windows.Threading;
using TaskIsland.Windows;

namespace TaskIsland.App.Services;

/// <summary>
/// Efficiency mode (EcoQoS) while collapsed (PLAN.md section 6.7 rule 7): on after 10 s collapsed and not hovered,
/// off at once when the user reaches for the island. One one-shot timer. Failure is logged once and ignored.
/// </summary>
public sealed class EfficiencyMode
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(10);
    private readonly DispatcherTimer _timer;
    private bool _on;
    private bool _failed;

    public EfficiencyMode()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = Delay };
        _timer.Tick += (_, _) => { _timer.Stop(); Set(true); };
    }

    public bool IsOn => _on;

    /// <summary>The island is resting: switch Efficiency mode on after 10 s unless something cancels it.</summary>
    public void ScheduleOn()
    {
        if (_on || _failed) return;
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>The user is reaching for the island: full speed now.</summary>
    public void Off()
    {
        _timer.Stop();
        if (_on) Set(false);
    }

    private void Set(bool on)
    {
        try
        {
            if (ProcessPower.SetEfficiencyMode(on)) { _on = on; return; }
            Fail($"SetProcessInformation returned false (error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()})");
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
        }
    }

    private void Fail(string reason)
    {
        if (_failed) return;
        _failed = true;
        _on = false;
        // FileLogger arrives in Phase 4; until then the reason goes to the debug output.
        Trace.WriteLine($"TaskIsland: Efficiency mode unavailable: {reason}");
    }
}
