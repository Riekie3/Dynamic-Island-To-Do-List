using TaskIsland.Core.Mascot;
using TaskIsland.Core.Models;

namespace TaskIsland.Core.Tests;

public class ReactionRulesTests
{
    [Fact]
    public void Random_pool_never_contains_alerts()
    {
        Assert.DoesNotContain(Reaction.Sad, ReactionRules.RandomPool);
        Assert.DoesNotContain(Reaction.Surprised, ReactionRules.RandomPool);
        Assert.Equal(7, ReactionRules.RandomPool.Count);
    }

    [Fact]
    public void Random_pick_never_repeats_and_stays_in_the_pool()
    {
        var rng = new Random(42);
        Reaction? last = null;
        var seen = new HashSet<Reaction>();
        for (int i = 0; i < 2000; i++)
        {
            var pick = ReactionRules.PickRandom(rng, last);
            Assert.NotEqual(last, pick);
            Assert.Contains(pick, ReactionRules.RandomPool);
            seen.Add(pick);
            last = pick;
        }
        Assert.Equal(ReactionRules.RandomPool.Count, seen.Count);
    }

    [Theory]
    [InlineData(IdleReactions.Rare, 10, 20)]
    [InlineData(IdleReactions.Normal, 4, 8)]
    [InlineData(IdleReactions.Lively, 1, 3)]
    public void Pop_delay_stays_inside_the_window(IdleReactions level, int minMinutes, int maxMinutes)
    {
        var rng = new Random(7);
        for (int i = 0; i < 1000; i++)
        {
            var d = ReactionRules.NextPopDelay(level, rng);
            Assert.NotNull(d);
            Assert.InRange(d!.Value, TimeSpan.FromMinutes(minMinutes), TimeSpan.FromMinutes(maxMinutes));
        }
    }

    [Fact]
    public void Pops_off_gives_no_delay()
    {
        Assert.Null(ReactionRules.NextPopDelay(IdleReactions.Off, new Random(1)));
        Assert.Null(ReactionRules.PopWindow(IdleReactions.Off));
    }

    [Fact]
    public void Anything_shows_when_nothing_is_showing()
    {
        Assert.True(ReactionRules.ShouldShow(Reaction.Love, true, null, false));
        Assert.True(ReactionRules.ShouldShow(Reaction.Happy, false, null, false));
    }

    [Fact]
    public void Random_pops_are_dropped_while_anything_shows()
    {
        Assert.False(ReactionRules.ShouldShow(Reaction.Wink, true, Reaction.Neutral, true));
        Assert.False(ReactionRules.ShouldShow(Reaction.Wink, true, Reaction.Happy, false));
    }

    [Fact]
    public void Events_replace_random_pops_and_other_events()
    {
        Assert.True(ReactionRules.ShouldShow(Reaction.Happy, false, Reaction.Love, true));
        Assert.True(ReactionRules.ShouldShow(Reaction.Wink, false, Reaction.Happy, false));
    }

    [Fact]
    public void Alerts_beat_everything_and_Thinking_beats_task_events()
    {
        Assert.True(ReactionRules.ShouldShow(Reaction.Sad, false, Reaction.Thinking, false));
        Assert.True(ReactionRules.ShouldShow(Reaction.Surprised, false, Reaction.Sad, false));
        Assert.False(ReactionRules.ShouldShow(Reaction.Happy, false, Reaction.Sad, false));
        Assert.False(ReactionRules.ShouldShow(Reaction.Happy, false, Reaction.Thinking, false));
        Assert.True(ReactionRules.ShouldShow(Reaction.Thinking, false, Reaction.Happy, false));
    }

    [Fact]
    public void Random_Thinking_pop_ranks_lowest()
    {
        Assert.Equal(0, ReactionRules.Rank(Reaction.Thinking, isRandom: true));
        Assert.True(ReactionRules.ShouldShow(Reaction.Happy, false, Reaction.Thinking, true));
    }

    [Theory]
    [InlineData(Reaction.Surprised, 5)]
    [InlineData(Reaction.Sad, 5)]
    [InlineData(Reaction.Happy, 2.5)]
    [InlineData(Reaction.Neutral, 2.5)]
    public void Alerts_hold_longer(Reaction reaction, double seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), ReactionRules.HoldTime(reaction));
    }

    [Fact]
    public void Every_reaction_has_a_screen_reader_description()
    {
        foreach (var r in Enum.GetValues<Reaction>())
            Assert.StartsWith("TaskIsland", ReactionRules.Describe(r));
    }
}
