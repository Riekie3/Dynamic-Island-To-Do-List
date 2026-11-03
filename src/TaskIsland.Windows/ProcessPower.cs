using System.Runtime.InteropServices;
using TaskIsland.Windows.Native;

namespace TaskIsland.Windows;

/// <summary>Power hints for our own process only (PLAN.md section 6.7 rules 7 and 8).</summary>
public static class ProcessPower
{
    /// <summary>
    /// Turns EcoQoS ("Efficiency mode") on or off for this process. Returns false if Windows refused; the caller
    /// logs that once and carries on.
    /// </summary>
    public static bool SetEfficiencyMode(bool on)
    {
        var state = new Kernel32.PROCESS_POWER_THROTTLING_STATE
        {
            Version = Kernel32.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
            ControlMask = Kernel32.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            StateMask = on ? Kernel32.PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0,
        };
        return Kernel32.SetProcessInformation(Kernel32.GetCurrentProcess(), Kernel32.ProcessPowerThrottling,
            ref state, Marshal.SizeOf<Kernel32.PROCESS_POWER_THROTTLING_STATE>());
    }

    /// <summary>True when Windows Battery saver (Energy saver) is on.</summary>
    public static bool IsBatterySaverOn() =>
        Kernel32.GetSystemPowerStatus(out var s) && (s.SystemStatusFlag & 1) != 0;
}
