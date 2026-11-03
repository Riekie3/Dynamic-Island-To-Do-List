using TaskIsland.Core.Models;

namespace TaskIsland.Core.Mascot;

/// <summary>One small idle movement of the face (PLAN.md section 6.8, companion). Each plays once and ends.</summary>
public enum LifeMove { Blink, DoubleBlink, GlanceLeft, GlanceRight, Breathe, Squish, MouthTwitch, AccessoryWiggle }

/// <summary>
/// Pure rules for the companion's idle life: how long to wait before the next movement and which movement suits the
/// face that is showing. The app's ReactionDirector owns the one-shot timer; the app's FaceRig plays the movement.
/// </summary>
public static class CompanionRules
{
    /// <summary>Faces whose eyes are open shapes, so a blink or a glance reads well.</summary>
    private static readonly HashSet<Reaction> OpenEyes =
        [Reaction.Neutral, Reaction.Curious, Reaction.Surprised, Reaction.Thinking, Reaction.Focused, Reaction.Love];

    /// <summary>Faces with an accessory (zZ, sparkles, marks) that may wiggle.</summary>
    private static readonly HashSet<Reaction> WithAccessory =
        [Reaction.Laughing, Reaction.Surprised, Reaction.Curious, Reaction.Sleepy, Reaction.Thinking, Reaction.Excited,
         Reaction.Focused, Reaction.Sad];

    /// <summary>Faces with no mouth.</summary>
    private static readonly HashSet<Reaction> NoMouth = [Reaction.Neutral, Reaction.Curious, Reaction.Sleepy, Reaction.Focused];

    /// <summary>Shortest and longest wait before the next movement. Null when the companion is off.</summary>
    public static (TimeSpan Min, TimeSpan Max)? Window(CompanionLevel level, bool faceShowing) => (level, faceShowing) switch
    {
        (CompanionLevel.Calm, false) => (TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(9)),
        (CompanionLevel.Calm, true) => (TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(3.5)),
        (CompanionLevel.Lively, false) => (TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(6)),
        (CompanionLevel.Lively, true) => (TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2.5)),
        _ => null,
    };

    /// <summary>A random delay inside <see cref="Window"/>, or null when the companion is off.</summary>
    public static TimeSpan? NextDelay(CompanionLevel level, bool faceShowing, Random random)
    {
        if (Window(level, faceShowing) is not { } w) return null;
        return w.Min + TimeSpan.FromMilliseconds(random.NextDouble() * (w.Max - w.Min).TotalMilliseconds);
    }

    /// <summary>The movements that suit what is showing: the Mini dots (<paramref name="face"/> null) or a face.</summary>
    public static IReadOnlyList<(LifeMove Move, int Weight)> Candidates(Reaction? face, bool sleepy)
    {
        if (face is null)
        {
            // Sleeping dots only breathe; awake dots blink and look around.
            if (sleepy) return [(LifeMove.Breathe, 1)];
            return [(LifeMove.Blink, 45), (LifeMove.DoubleBlink, 15), (LifeMove.GlanceLeft, 13), (LifeMove.GlanceRight, 13),
                    (LifeMove.Breathe, 14)];
        }
        var f = face.Value;
        var list = new List<(LifeMove, int)> { (LifeMove.Breathe, 10) };
        if (OpenEyes.Contains(f))
        {
            list.Add((LifeMove.Blink, 35));
            list.Add((LifeMove.DoubleBlink, 10));
            list.Add((LifeMove.GlanceLeft, 10));
            list.Add((LifeMove.GlanceRight, 10));
        }
        else if (f is not (Reaction.Sleepy or Reaction.Sad))
        {
            // Closed or squeezed eyes (Happy, Laughing, Wink, Excited): a happy squeeze instead of a blink.
            list.Add((LifeMove.Squish, 25));
        }
        if (!NoMouth.Contains(f)) list.Add((LifeMove.MouthTwitch, 15));
        if (WithAccessory.Contains(f)) list.Add((LifeMove.AccessoryWiggle, 15));
        return list;
    }

    /// <summary>Picks a weighted movement for what is showing, avoiding the same one twice in a row when possible.</summary>
    public static LifeMove Pick(Random random, Reaction? face, bool sleepy, LifeMove? last)
    {
        var all = Candidates(face, sleepy);
        var pool = all.Count > 1 && last is { } l ? all.Where(c => c.Move != l).ToList() : all.ToList();
        int total = pool.Sum(c => c.Weight);
        int roll = random.Next(total);
        foreach (var (move, weight) in pool)
        {
            if (roll < weight) return move;
            roll -= weight;
        }
        return pool[^1].Move;
    }
}
