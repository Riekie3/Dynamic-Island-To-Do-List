using System.Windows.Media.Animation;

namespace TaskIsland.App.Controls;

/// <summary>
/// cubic-bezier easing (PLAN.md section 6.0). <see cref="Decelerate"/> is cubic-bezier(0.2, 0, 0, 1), "emphasised
/// decelerate", used for everything that grows or appears; <see cref="Accelerate"/> is its mirror for collapsing.
/// No overshoot.
/// </summary>
public sealed class FluentEase(double x1, double y1, double x2, double y2) : IEasingFunction
{
    public static readonly FluentEase Decelerate = new(0.2, 0, 0, 1);
    public static readonly FluentEase Accelerate = new(1, 0, 0.8, 1);

    public double Ease(double normalizedTime)
    {
        double x = Math.Clamp(normalizedTime, 0, 1);
        if (x is 0 or 1) return x;
        return Bezier(SolveT(x), y1, y2);
    }

    private static double Bezier(double t, double p1, double p2)
    {
        double u = 1 - t;
        return 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t;
    }

    private static double Slope(double t, double p1, double p2)
    {
        double u = 1 - t;
        return 3 * u * u * p1 + 6 * u * t * (p2 - p1) + 3 * t * t * (1 - p2);
    }

    /// <summary>Finds t for a given x: Newton steps, falling back to bisection where the slope is flat.</summary>
    private double SolveT(double x)
    {
        double t = x;
        for (int i = 0; i < 8; i++)
        {
            double err = Bezier(t, x1, x2) - x;
            if (Math.Abs(err) < 1e-6) return t;
            double d = Slope(t, x1, x2);
            if (Math.Abs(d) < 1e-6) break;
            t -= err / d;
        }
        double lo = 0, hi = 1;
        t = x;
        for (int i = 0; i < 30; i++)
        {
            double v = Bezier(t, x1, x2);
            if (Math.Abs(v - x) < 1e-6) break;
            if (v < x) lo = t; else hi = t;
            t = (lo + hi) / 2;
        }
        return t;
    }
}
