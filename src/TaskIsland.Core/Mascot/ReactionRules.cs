using TaskIsland.Core.Models;

namespace TaskIsland.Core.Mascot;

/// <summary>The 12 mascot expressions (PLAN.md section 6.8), in the order of the reference sheet.</summary>
public enum Reaction { Neutral, Happy, Laughing, Wink, Surprised, Curious, Sleepy, Thinking, Love, Excited, Focused, Sad }

/// <summary>
/// Pure rules for which face may show (PLAN.md section 6.8): priorities, random pop choice and timing, hold times.
/// The app's ReactionDirector owns the timers; everything that can be decided without a clock lives here.
/// </summary>
public static class ReactionRules
{
    /// <summary>Faces a random pop may pick. Never Sad or Surprised.</summary>
    public static readonly IReadOnlyList<Reaction> RandomPool =
    [
        Reaction.Neutral, Reaction.Happy, Reaction.Laughing, Reaction.Wink, Reaction.Curious, Reaction.Love,
        Reaction.Thinking,
    ];

    /// <summary>Random pops 0, normal events 1, Thinking 2, alerts (Sad, Surprised) 3.</summary>
    public static int Rank(Reaction reaction, bool isRandom) =>
        isRandom ? 0 : reaction switch
        {
            Reaction.Sad or Reaction.Surprised => 3,
            Reaction.Thinking => 2,
            _ => 1,
        };

    /// <summary>
    /// Whether a requested face may replace what is showing. Random pops are dropped (not queued) when anything else
    /// shows; other requests replace a face of the same or lower rank.
    /// </summary>
    public static bool ShouldShow(Reaction requested, bool requestedIsRandom, Reaction? current, bool currentIsRandom)
    {
        if (current is null) return true;
        if (requestedIsRandom) return false;
        return Rank(requested, false) >= Rank(current.Value, currentIsRandom);
    }

    /// <summary>Picks a random-pop face, never the same as <paramref name="last"/>.</summary>
    public static Reaction PickRandom(Random random, Reaction? last)
    {
        while (true)
        {
            var pick = RandomPool[random.Next(RandomPool.Count)];
            if (pick != last) return pick;
        }
    }

    /// <summary>Shortest and longest wait before the next random pop. Null when pops are off.</summary>
    public static (TimeSpan Min, TimeSpan Max)? PopWindow(IdleReactions level) => level switch
    {
        IdleReactions.Rare => (TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(20)),
        IdleReactions.Normal => (TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(8)),
        IdleReactions.Lively => (TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(3)),
        _ => null,
    };

    /// <summary>A random delay inside <see cref="PopWindow"/>, or null when pops are off.</summary>
    public static TimeSpan? NextPopDelay(IdleReactions level, Random random)
    {
        if (PopWindow(level) is not { } w) return null;
        var span = (w.Max - w.Min).TotalMilliseconds;
        return w.Min + TimeSpan.FromMilliseconds(random.NextDouble() * span);
    }

    /// <summary>How long a reaction stays before returning to the Mini notch. Alerts hold 5 s, others 2.5 s.</summary>
    public static TimeSpan HoldTime(Reaction reaction) =>
        reaction is Reaction.Surprised or Reaction.Sad ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(2.5);

    /// <summary>Untouched this long, the Mini notch falls asleep.</summary>
    public static readonly TimeSpan SleepyAfter = TimeSpan.FromMinutes(60);

    /// <summary>No random pops and no greeting this long after a <c>--startup</c> launch.</summary>
    public static readonly TimeSpan StartupQuietPeriod = TimeSpan.FromSeconds(60);

    /// <summary>Text for screen readers (the face itself is decorative).</summary>
    public static string Describe(Reaction reaction) => reaction switch
    {
        Reaction.Neutral => "TaskIsland",
        Reaction.Happy => "TaskIsland: task completed",
        Reaction.Laughing => "TaskIsland: laughing",
        Reaction.Wink => "TaskIsland: hello",
        Reaction.Surprised => "TaskIsland: important alert",
        Reaction.Curious => "TaskIsland: curious",
        Reaction.Sleepy => "TaskIsland: sleeping",
        Reaction.Thinking => "TaskIsland: working",
        Reaction.Love => "TaskIsland: well done",
        Reaction.Excited => "TaskIsland: all done",
        Reaction.Focused => "TaskIsland: focused",
        Reaction.Sad => "TaskIsland: something went wrong",
        _ => "TaskIsland",
    };
}
