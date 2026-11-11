using TaskIsland.Core.Services;

namespace TaskIsland.Core.Tests;

/// <summary>Drag to place (docs/DRAG-PLACE-PLAN.md): the offset saved for a dragged pill.</summary>
public class DragPlaceTests
{
    private static readonly PixelRect FullHd = new(0, 0, 1920, 1080);

    [Fact]
    public void Centre_of_the_monitor_is_offset_zero()
        => Assert.Equal(0, IslandPlacement.OffsetForCentre(FullHd, 1.0, 960, 420));

    [Theory]
    [InlineData(1.0, 760, -200)]
    [InlineData(1.25, 710, -200)]
    [InlineData(1.5, 1260, 200)]
    public void Offset_is_in_dips_at_any_scaling(double scale, double centreX, int expected)
        => Assert.Equal(expected, IslandPlacement.OffsetForCentre(FullHd, scale, centreX, 420));

    [Fact]
    public void Dragging_past_the_edges_stops_where_the_open_panel_still_fits()
    {
        // 1920 wide at 100 %: the panel (420) fits while the centre is between 210 and 1710, so the offset is ±750.
        Assert.Equal(-750, IslandPlacement.OffsetForCentre(FullHd, 1.0, -500, 420));
        Assert.Equal(750, IslandPlacement.OffsetForCentre(FullHd, 1.0, 5000, 420));
    }

    [Fact]
    public void A_monitor_left_of_the_primary_uses_its_own_centre()
    {
        var left = new PixelRect(-2560, 0, 0, 1440);
        Assert.Equal(0, IslandPlacement.OffsetForCentre(left, 1.0, -1280, 420));
        Assert.Equal(-300, IslandPlacement.OffsetForCentre(left, 1.0, -1580, 420));
    }

    [Fact]
    public void The_saved_offset_puts_the_island_back_in_the_same_place()
    {
        foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0 })
        {
            int offset = IslandPlacement.OffsetForCentre(FullHd, scale, 1300, 420);
            var rect = IslandPlacement.Compute(FullHd, scale, 280, 64, 420, offset, 0);
            Assert.InRange(rect.Left + rect.Width / 2.0, 1300 - scale, 1300 + scale);
        }
    }

    [Fact]
    public void A_monitor_too_narrow_for_the_panel_keeps_the_island_centred()
        => Assert.Equal(0, IslandPlacement.OffsetForCentre(new PixelRect(0, 0, 400, 300), 1.0, 50, 420));
}
