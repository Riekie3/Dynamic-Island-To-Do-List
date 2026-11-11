namespace TaskIsland.Core.Toasts;

/// <summary>
/// A damped spring as a 0..1 easing curve (SILEO-PLAN.md section 3; Sileo uses bounce 0.25 over 600 ms).
/// <paramref name="bounce"/> 0 settles without overshoot; higher values overshoot and swing back once or twice.
/// The curve is scaled so it has settled (within 0.1 %) at t = 1, and <see cref="Ease"/>(1) is exactly 1.
/// </summary>
public static class Spring
{
    /// <summary>Remaining distance at t = 1: e^-k = 0.001.</summary>
    private static readonly double Settle = Math.Log(1000);

    /// <summary>Sileo's default.</summary>
    public const double DefaultBounce = 0.25;

    public static double Ease(double t, double bounce = DefaultBounce)
    {
        if (t <= 0) return 0;
        if (t >= 1) return 1;
        double zeta = 1 - Math.Clamp(bounce, 0, 0.95);   // damping ratio
        if (zeta >= 1)
        {
            // Critically damped: x = 1 - e^(-wt)(1 + wt). Solve w so the remainder at t = 1 is about 0.001.
            const double w = 9.233;
            return 1 - Math.Exp(-w * t) * (1 + w * t);
        }
        double omega = Settle / zeta;                     // envelope e^(-zeta*omega*t) reaches 0.001 at t = 1
        double wd = omega * Math.Sqrt(1 - zeta * zeta);   // damped frequency
        double env = Math.Exp(-zeta * omega * t);
        return 1 - env * (Math.Cos(wd * t) + zeta * omega / wd * Math.Sin(wd * t));
    }

    /// <summary>The largest value the curve reaches (1 + overshoot), sampled.</summary>
    public static double Peak(double bounce = DefaultBounce)
    {
        double max = 0;
        for (int i = 0; i <= 1000; i++) max = Math.Max(max, Ease(i / 1000.0, bounce));
        return max;
    }
}
