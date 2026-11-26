using TaskIsland.Core.Services;

namespace TaskIsland.Core.Tests;

/// <summary>
/// Drag to place (docs/DRAG-PLACE-PLAN.md): the resting pill may sit anywhere on its monitor, snaps to a nearby edge
/// on release and comes back to the same place after a restart.
/// </summary>
public class DragPlaceTests
{
    private static readonly PixelRect FullHd = new(0, 0, 1920, 1080);
    private const double PillW = 96, PillH = 25.6;

    [Fact]
    public void A_top_centred_pill_is_offset_zero()
    {
        var pill = IslandPlacement.PillAt(FullHd, 1.0, PillW, PillH, 0, 0);
        Assert.Equal(912, pill.LeftDip, 3);
        Assert.Equal(0, pill.TopDip, 3);
        Assert.Equal(IslandEdge.Top, pill.Edge);
        Assert.Equal((0, 0), IslandPlacement.OffsetsFor(FullHd, 1.0, pill));
    }

    [Theory]
    [InlineData(1.0, 760, 300, -152, 300)]
    [InlineData(1.25, 600, 240, -120, 240)]
    [InlineData(1.5, 900, 480, 308, 480)]
    [InlineData(2.0, 400, 300, -32, 300)]
    public void Position_is_saved_in_dips_at_any_scaling(double scale, double leftDip, double topDip,
        int expectedOffset, int expectedTop)
    {
        var pill = IslandPlacement.PillAt(FullHd, scale, PillW, PillH, 0, 0);
        pill = pill with { LeftDip = leftDip, TopDip = topDip };
        Assert.Equal((expectedOffset, expectedTop), IslandPlacement.OffsetsFor(FullHd, scale, pill));
    }

    [Fact]
    public void Drag_snaps_to_each_edge()
    {
        var top = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, 800, 10, 24);
        Assert.Equal(IslandEdge.Top, top.Edge);
        Assert.Equal(0, top.TopDip, 3);

        var bottom = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, 800, 1040, 24);
        Assert.Equal(IslandEdge.Bottom, bottom.Edge);
        Assert.Equal(1080 - PillH, bottom.TopDip, 3);

        var left = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, 12, 300, 24);
        Assert.Equal(IslandEdge.Left, left.Edge);
        Assert.Equal(0, left.LeftDip, 3);

        var right = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, 1820, 300, 24);
        Assert.Equal(IslandEdge.Right, right.Edge);
        Assert.Equal(1920 - PillW, right.LeftDip, 3);
    }

    [Fact]
    public void A_drop_away_from_every_edge_stays_free_and_round()
    {
        var pill = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, 500, 400, 24);
        Assert.Equal(IslandEdge.None, pill.Edge);
        Assert.Equal(500, pill.LeftDip, 3);
        Assert.Equal(400, pill.TopDip, 3);
    }

    [Fact]
    public void A_pill_dragged_past_the_monitor_is_kept_on_it()
    {
        var left = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, -200, 400, 0);
        Assert.Equal(0, left.LeftDip, 3);
        Assert.Equal(IslandEdge.Left, left.Edge);

        var bottom = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, 500, 5000, 0);
        Assert.Equal(1080 - PillH, bottom.TopDip, 3);
        Assert.Equal(IslandEdge.Bottom, bottom.Edge);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void A_free_position_round_trips(double scale)
    {
        var pill = IslandPlacement.Snap(FullHd, scale, PillW, PillH, 700, 400, 24);
        Assert.Equal(IslandEdge.None, pill.Edge);
        var (offset, top) = IslandPlacement.OffsetsFor(FullHd, scale, pill);
        var back = IslandPlacement.PillAt(FullHd, scale, PillW, PillH, offset, top);
        Assert.Equal(pill.LeftDip, back.LeftDip, 1);
        Assert.Equal(pill.TopDip, back.TopDip, 1);
        Assert.Equal(IslandEdge.None, back.Edge);
    }

    [Theory]
    [InlineData(IslandEdge.Top)]
    [InlineData(IslandEdge.Bottom)]
    [InlineData(IslandEdge.Left)]
    [InlineData(IslandEdge.Right)]
    public void A_saved_edge_pill_restores_the_same_edge(IslandEdge edge)
    {
        var (left, top) = edge switch
        {
            IslandEdge.Top => (800.0, 0.0),
            IslandEdge.Bottom => (800.0, 1080 - PillH),
            IslandEdge.Left => (0.0, 300.0),
            _ => (1920 - PillW, 300.0),
        };
        var pill = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, left, top, 0);
        Assert.Equal(edge, pill.Edge);
        var (offset, margin) = IslandPlacement.OffsetsFor(FullHd, 1.0, pill);
        var back = IslandPlacement.PillAt(FullHd, 1.0, PillW, PillH, offset, margin);
        Assert.Equal(edge, back.Edge);
        Assert.Equal(pill.LeftDip, back.LeftDip, 1);
        Assert.Equal(pill.TopDip, back.TopDip, 1);
    }

    [Fact]
    public void A_monitor_left_of_the_primary_uses_its_own_origin()
    {
        var left = new PixelRect(-2560, 0, 0, 1440);
        var pill = IslandPlacement.Snap(left, 1.0, PillW, PillH, 100, 500, 24);
        var (offset, top) = IslandPlacement.OffsetsFor(left, 1.0, pill);
        Assert.Equal(-1132, offset); // 100 + 48 DIPs from that monitor's left, centre at 1280
        Assert.Equal(500, top);
        var back = IslandPlacement.PillAt(left, 1.0, PillW, PillH, offset, top);
        Assert.Equal(pill.LeftDip, back.LeftDip, 3);
    }

    [Fact]
    public void A_monitor_too_narrow_for_the_pill_keeps_it_centred()
    {
        var tiny = new PixelRect(0, 0, 60, 200);
        var pill = IslandPlacement.Snap(tiny, 1.0, PillW, PillH, 0, 100, 0);
        Assert.Equal((60 - PillW) / 2, pill.LeftDip, 3);
        Assert.Equal(IslandEdge.None, pill.Edge);
    }

    [Fact]
    public void The_window_follows_a_floating_pill()
    {
        var pill = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, 500, 300, 0);
        var win = IslandPlacement.WindowFor(FullHd, 1.0, pill, 452, 64, 420, false, false);
        Assert.Equal(new PixelRect(322, 300, 774, 364), win.Bounds);
        Assert.Equal(226, win.CompactCentreDip, 3);
        Assert.False(win.BottomAnchored);
        Assert.False(win.OpenUp);
        Assert.Equal(IslandEdge.None, win.Edge);
    }

    [Fact]
    public void The_open_panel_slides_inside_the_monitor()
    {
        // 124 DIPs from the right edge: the 420-wide panel is pulled in (the window overhangs by 16 DIPs, transparent).
        var pill = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, 1700, 400, 0);
        var win = IslandPlacement.WindowFor(FullHd, 1.0, pill, 452, 600, 420, true, false);
        Assert.Equal(1484, win.Bounds.Left);
        Assert.Equal(1500, win.Bounds.Left + (452 - 420) / 2.0, 3);
        Assert.Equal(1920, win.Bounds.Left + (452 + 420) / 2.0, 3);
        Assert.Equal(400, win.Bounds.Top);
        Assert.Equal(600, win.Bounds.Height);
    }

    [Fact]
    public void A_bottom_pill_opens_upwards_from_the_same_bottom_edge()
    {
        var pill = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, 800, 1040, 24);
        Assert.Equal(IslandEdge.Bottom, pill.Edge);
        Assert.True(IslandPlacement.OpensUp(FullHd, 1.0, pill, 550));
        var win = IslandPlacement.WindowFor(FullHd, 1.0, pill, 452, 600, 420, true, true);
        Assert.True(win.BottomAnchored);
        Assert.True(win.OpenUp);
        Assert.Equal(1080, win.Bounds.Bottom);
        Assert.Equal(1080 - 600, win.Bounds.Top);
    }

    [Fact]
    public void A_side_pill_keeps_the_window_flush_with_that_side()
    {
        var pill = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, 0, 300, 0);
        Assert.Equal(IslandEdge.Left, pill.Edge);
        var small = IslandPlacement.WindowFor(FullHd, 1.0, pill, 452, 64, 420, false, false);
        Assert.Equal(0, small.Bounds.Left);
        Assert.Equal(48, small.CompactCentreDip, 3);

        var large = IslandPlacement.WindowFor(FullHd, 1.0, pill, 452, 600, 420, true, false);
        Assert.Equal(0, large.Bounds.Left);
        Assert.Equal(48, large.CompactCentreDip, 3);
        Assert.Equal(new PixelRect(0, 300, 452, 900), large.Bounds);
    }

    [Theory]
    [InlineData(400, false)] // plenty of room below
    [InlineData(500, false)] // 580 DIPs below still fit the 550 DIP panel
    [InlineData(550, true)]  // only 530 below: open upwards
    [InlineData(800, true)]
    public void The_panel_opens_up_only_when_it_does_not_fit_below(double top, bool expected)
    {
        var pill = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, 800, top, 0);
        Assert.Equal(expected, IslandPlacement.OpensUp(FullHd, 1.0, pill, 550));
    }

    [Fact]
    public void A_short_monitor_opens_towards_the_larger_half()
    {
        var screen = new PixelRect(0, 0, 1920, 600);
        var low = IslandPlacement.Snap(screen, 1.0, PillW, PillH, 800, 300, 0);
        Assert.True(IslandPlacement.OpensUp(screen, 1.0, low, 550));
        var high = IslandPlacement.Snap(screen, 1.0, PillW, PillH, 800, 200, 0);
        Assert.False(IslandPlacement.OpensUp(screen, 1.0, high, 550));
    }

    [Fact]
    public void The_panel_keeps_its_full_height_when_the_pill_has_room()
    {
        var top = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, 800, 0, 0);
        Assert.Equal(550, IslandPlacement.PanelHeightLimit(FullHd, 1.0, top, false, 550), 3);
        var bottom = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, 800, 1080 - PillH, 0);
        Assert.Equal(550, IslandPlacement.PanelHeightLimit(FullHd, 1.0, bottom, true, 550), 3);
    }

    [Fact]
    public void A_pill_near_the_middle_shortens_the_panel_to_the_room_it_has()
    {
        // 1920x1080 at 125 % is 864 DIP tall. 400 DIPs from the top leaves 464 below and 425.6 above: the panel
        // opens down (the larger side) and is shortened to 464, ending exactly at the screen bottom.
        var down = IslandPlacement.Snap(FullHd, 1.25, PillW, PillH, 800, 400, 0);
        Assert.False(IslandPlacement.OpensUp(FullHd, 1.25, down, 550));
        Assert.Equal(464, IslandPlacement.PanelHeightLimit(FullHd, 1.25, down, false, 550), 3);
        Assert.Equal(864, down.TopDip + 464, 3);

        // At 500 DIPs the larger room is above (525.6): it opens up and starts exactly at the screen top.
        var up = IslandPlacement.Snap(FullHd, 1.25, PillW, PillH, 800, 500, 0);
        Assert.True(IslandPlacement.OpensUp(FullHd, 1.25, up, 550));
        Assert.Equal(525.6, IslandPlacement.PanelHeightLimit(FullHd, 1.25, up, true, 550), 3);
        Assert.Equal(0, up.TopDip + PillH - 525.6, 3);
    }

    [Fact]
    public void The_panel_limit_uses_the_monitor_scaling()
    {
        var at100 = IslandPlacement.Snap(FullHd, 1.0, PillW, PillH, 800, 400, 0);
        var at125 = IslandPlacement.Snap(FullHd, 1.25, PillW, PillH, 800, 400, 0);
        Assert.Equal(680, IslandPlacement.PanelHeightLimit(FullHd, 1.0, at100, false, 700), 3);
        Assert.Equal(464, IslandPlacement.PanelHeightLimit(FullHd, 1.25, at125, false, 700), 3);
    }
}
