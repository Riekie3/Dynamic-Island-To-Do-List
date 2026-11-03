using TaskIsland.Core.Mascot;
using TaskIsland.Core.Models;

namespace TaskIsland.Core.Tests;

public class CompanionRulesTests
{
    [Fact]
    public void Off_has_no_window_and_no_delay()
    {
        Assert.Null(CompanionRules.Window(CompanionLevel.Off, faceShowing: false));
        Assert.Null(CompanionRules.Window(CompanionLevel.Off, faceShowing: true));
        Assert.Null(CompanionRules.NextDelay(CompanionLevel.Off, false, new Random(1)));
    }

    [Theory]
    [InlineData(CompanionLevel.Calm, false, 4000, 9000)]
    [InlineData(CompanionLevel.Calm, true, 1500, 3500)]
    [InlineData(CompanionLevel.Lively, false, 2500, 6000)]
    [InlineData(CompanionLevel.Lively, true, 1000, 2500)]
    public void Delay_stays_inside_the_window(CompanionLevel level, bool face, int minMs, int maxMs)
    {
        var random = new Random(7);
        for (int i = 0; i < 500; i++)
        {
            var d = CompanionRules.NextDelay(level, face, random)!.Value.TotalMilliseconds;
            Assert.InRange(d, minMs, maxMs);
        }
    }

    [Fact]
    public void Faces_move_more_often_than_the_resting_dots()
    {
        foreach (var level in new[] { CompanionLevel.Calm, CompanionLevel.Lively })
            Assert.True(CompanionRules.Window(level, true)!.Value.Max <= CompanionRules.Window(level, false)!.Value.Min);
    }

    [Fact]
    public void Sleeping_dots_only_breathe()
    {
        var random = new Random(3);
        for (int i = 0; i < 100; i++)
            Assert.Equal(LifeMove.Breathe, CompanionRules.Pick(random, null, sleepy: true, last: LifeMove.Breathe));
    }

    [Fact]
    public void Awake_dots_mostly_blink_and_never_use_face_parts()
    {
        var moves = CompanionRules.Candidates(null, sleepy: false).Select(c => c.Move).ToList();
        Assert.Contains(LifeMove.Blink, moves);
        Assert.Contains(LifeMove.GlanceLeft, moves);
        Assert.DoesNotContain(LifeMove.MouthTwitch, moves);
        Assert.DoesNotContain(LifeMove.AccessoryWiggle, moves);
        Assert.DoesNotContain(LifeMove.Squish, moves);
    }

    [Theory]
    [InlineData(Reaction.Neutral)]
    [InlineData(Reaction.Curious)]
    [InlineData(Reaction.Focused)]
    [InlineData(Reaction.Sleepy)]
    public void Faces_without_a_mouth_never_twitch_it(Reaction face)
    {
        Assert.DoesNotContain(CompanionRules.Candidates(face, false), c => c.Move == LifeMove.MouthTwitch);
    }

    [Theory]
    [InlineData(Reaction.Neutral)]
    [InlineData(Reaction.Happy)]
    [InlineData(Reaction.Wink)]
    [InlineData(Reaction.Love)]
    public void Faces_without_an_accessory_never_wiggle_one(Reaction face)
    {
        Assert.DoesNotContain(CompanionRules.Candidates(face, false), c => c.Move == LifeMove.AccessoryWiggle);
    }

    [Theory]
    [InlineData(Reaction.Happy)]
    [InlineData(Reaction.Laughing)]
    [InlineData(Reaction.Wink)]
    [InlineData(Reaction.Excited)]
    [InlineData(Reaction.Sleepy)]
    [InlineData(Reaction.Sad)]
    public void Closed_or_squeezed_eyes_never_blink_or_glance(Reaction face)
    {
        var moves = CompanionRules.Candidates(face, false).Select(c => c.Move).ToList();
        Assert.DoesNotContain(LifeMove.Blink, moves);
        Assert.DoesNotContain(LifeMove.DoubleBlink, moves);
        Assert.DoesNotContain(LifeMove.GlanceLeft, moves);
        Assert.DoesNotContain(LifeMove.GlanceRight, moves);
    }

    [Fact]
    public void Sad_never_gets_the_happy_squeeze()
    {
        Assert.DoesNotContain(CompanionRules.Candidates(Reaction.Sad, false), c => c.Move == LifeMove.Squish);
    }

    [Fact]
    public void Every_face_has_at_least_two_movements()
    {
        foreach (var r in Enum.GetValues<Reaction>())
            Assert.True(CompanionRules.Candidates(r, false).Count >= 2, r.ToString());
    }

    [Fact]
    public void Pick_never_repeats_the_last_move_when_there_is_a_choice()
    {
        var random = new Random(11);
        LifeMove? last = null;
        foreach (var face in new Reaction?[] { null, Reaction.Neutral, Reaction.Happy, Reaction.Thinking, Reaction.Sad })
            for (int i = 0; i < 200; i++)
            {
                var pick = CompanionRules.Pick(random, face, false, last);
                Assert.NotEqual(last, pick);
                Assert.Contains(CompanionRules.Candidates(face, false), c => c.Move == pick);
                last = pick;
            }
    }

    [Fact]
    public void Blink_is_the_most_common_move_for_the_dots()
    {
        var random = new Random(5);
        var counts = new Dictionary<LifeMove, int>();
        for (int i = 0; i < 4000; i++)
        {
            var m = CompanionRules.Pick(random, null, false, null);
            counts[m] = counts.GetValueOrDefault(m) + 1;
        }
        Assert.Equal(LifeMove.Blink, counts.MaxBy(kv => kv.Value).Key);
    }

    [Theory]
    [InlineData(Reaction.Happy, CompanionLevel.Off, 2.5)]
    [InlineData(Reaction.Happy, CompanionLevel.Calm, 4)]
    [InlineData(Reaction.Happy, CompanionLevel.Lively, 6)]
    [InlineData(Reaction.Sad, CompanionLevel.Off, 5)]
    [InlineData(Reaction.Sad, CompanionLevel.Calm, 5)]
    [InlineData(Reaction.Surprised, CompanionLevel.Lively, 6)]
    public void Faces_hold_longer_with_the_companion_on(Reaction r, CompanionLevel level, double seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), ReactionRules.HoldTime(r, level));
    }
}
