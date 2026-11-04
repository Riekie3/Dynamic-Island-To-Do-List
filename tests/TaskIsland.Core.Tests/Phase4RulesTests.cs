using TaskIsland.Core.Services;
using static TaskIsland.Core.Tests.TestData;

namespace TaskIsland.Core.Tests;

/// <summary>Pure rules behind the Windows integration (PLAN.md section 7): hotkey text, Run key, placement, full screen, reminders.</summary>
public class Phase4RulesTests
{
    // ---- hotkey (section 7.8) ----

    [Theory]
    [InlineData("Ctrl+Alt+T", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x54u)]
    [InlineData("ctrl + alt + t", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x54u)]
    [InlineData("Control+Shift+5", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x35u)]
    [InlineData("Win+Alt+F12", HotkeyModifiers.Win | HotkeyModifiers.Alt, 0x7Bu)]
    [InlineData("Ctrl+Alt+Space", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x20u)]
    [InlineData("Ctrl+F1", HotkeyModifiers.Control, 0x70u)]
    public void Hotkey_parses(string text, HotkeyModifiers mods, uint vk)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var g));
        Assert.Equal(mods, g.Modifiers);
        Assert.Equal(vk, g.VirtualKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("T")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Ctrl+T")]
    [InlineData("Hyper+T")]
    [InlineData("Ctrl+Alt+F25")]
    [InlineData("Ctrl+Alt+Tab")]
    [InlineData("Ctrl+Alt+TT")]
    public void Hotkey_rejects_invalid_text(string? text)
    {
        Assert.False(HotkeyGesture.TryParse(text, out _));
    }

    [Fact]
    public void Hotkey_formats_canonically_and_round_trips()
    {
        Assert.True(HotkeyGesture.TryParse("alt+shift+ctrl+k", out var g));
        Assert.Equal("Ctrl+Alt+Shift+K", g.ToString());
        Assert.True(HotkeyGesture.TryParse(g.ToString(), out var again));
        Assert.Equal(g, again);
        Assert.Equal("Ctrl+Alt+T", HotkeyGesture.Default.ToString());
        Assert.Equal("Ctrl+Alt+F9", new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x78).ToString());
    }

    [Fact]
    public void Rebinding_needs_two_modifiers()
    {
        Assert.True(HotkeyGesture.Default.IsAllowedForRebind);
        HotkeyGesture.TryParse("Ctrl+K", out var one);
        Assert.False(one.IsAllowedForRebind);
        HotkeyGesture.TryParse("Ctrl+Shift+K", out var two);
        Assert.True(two.IsAllowedForRebind);
    }

    [Fact]
    public void Invalid_hotkey_setting_falls_back_to_ctrl_alt_t()
    {
        Assert.Equal(HotkeyGesture.Default, HotkeyGesture.ParseOrDefault("nonsense"));
        Assert.Equal(HotkeyGesture.Default, HotkeyGesture.ParseOrDefault(null));
        Assert.Equal("Ctrl+Shift+Y", HotkeyGesture.ParseOrDefault("Ctrl+Shift+Y").ToString());
    }

    // ---- Run key (sections 7.6 and 7.11 rule 5) ----

    private const string Cmd = "\"C:\\Apps\\TaskIsland\\TaskIsland.exe\" --startup";

    [Fact]
    public void Startup_command_quotes_the_path_and_adds_the_startup_flag()
    {
        Assert.Equal(Cmd, StartupPolicy.CommandFor(@"C:\Apps\TaskIsland\TaskIsland.exe"));
    }

    [Fact]
    public void First_run_with_the_setting_on_writes_the_value()
    {
        Assert.Equal(StartupAction.Write, StartupPolicy.OnLaunch(true, firstRun: true, null, Cmd));
    }

    [Fact]
    public void A_value_the_user_removed_is_not_added_again_silently()
    {
        Assert.Equal(StartupAction.None, StartupPolicy.OnLaunch(true, firstRun: false, null, Cmd));
    }

    [Fact]
    public void A_value_pointing_at_an_old_path_is_fixed()
    {
        Assert.Equal(StartupAction.Write, StartupPolicy.OnLaunch(true, false, "\"D:\\Old\\TaskIsland.exe\" --startup", Cmd));
    }

    [Fact]
    public void The_same_value_is_left_alone_ignoring_case_and_spaces()
    {
        Assert.Equal(StartupAction.None, StartupPolicy.OnLaunch(true, false, " " + Cmd.ToUpperInvariant() + " ", Cmd));
    }

    [Fact]
    public void Setting_off_removes_an_existing_value_and_does_nothing_otherwise()
    {
        Assert.Equal(StartupAction.Remove, StartupPolicy.OnLaunch(false, false, Cmd, Cmd));
        Assert.Equal(StartupAction.Remove, StartupPolicy.OnLaunch(false, true, Cmd, Cmd));
        Assert.Equal(StartupAction.None, StartupPolicy.OnLaunch(false, true, null, Cmd));
    }

    [Fact]
    public void Toggle_on_always_writes_a_missing_value_and_off_removes_it()
    {
        Assert.Equal(StartupAction.Write, StartupPolicy.OnToggle(true, null, Cmd));
        Assert.Equal(StartupAction.None, StartupPolicy.OnToggle(true, Cmd, Cmd));
        Assert.Equal(StartupAction.Write, StartupPolicy.OnToggle(true, "\"x.exe\"", Cmd));
        Assert.Equal(StartupAction.Remove, StartupPolicy.OnToggle(false, Cmd, Cmd));
        Assert.Equal(StartupAction.None, StartupPolicy.OnToggle(false, null, Cmd));
    }

    // ---- monitor choice and placement (section 7.2) ----

    private static readonly ScreenInfo Primary = new(@"\\.\DISPLAY1", new PixelRect(0, 0, 1920, 1080), 1.0, true);
    private static readonly ScreenInfo Right = new(@"\\.\DISPLAY2", new PixelRect(1920, 0, 4480, 1440), 1.5, false);

    [Fact]
    public void Named_monitor_is_chosen_else_the_primary()
    {
        var all = new[] { Right, Primary };
        Assert.Equal(Right, IslandPlacement.PickMonitor(all, @"\\.\display2"));
        Assert.Equal(Primary, IslandPlacement.PickMonitor(all, ""));
        Assert.Equal(Primary, IslandPlacement.PickMonitor(all, @"\\.\DISPLAY9")); // unplugged → primary
        Assert.Equal(Right, IslandPlacement.PickMonitor([Right], null));           // no primary flag → first
        Assert.Null(IslandPlacement.PickMonitor([], null));
    }

    [Fact]
    public void Collapsed_window_is_top_centred_at_100_percent()
    {
        var r = IslandPlacement.Compute(Primary.Bounds, 1.0, 452, 64, 420, 0, 0);
        Assert.Equal(new PixelRect(734, 0, 1186, 64), r);
    }

    [Fact]
    public void Placement_scales_to_physical_pixels_on_a_second_monitor()
    {
        var r = IslandPlacement.Compute(Right.Bounds, 1.5, 452, 600, 420, 0, 10);
        Assert.Equal(678, r.Width);
        Assert.Equal(900, r.Height);
        Assert.Equal(15, r.Top);                                   // 10 DIP margin at 150 %
        Assert.Equal(1920 + 2560 / 2, r.Left + r.Width / 2);       // centred on that monitor
    }

    [Fact]
    public void Both_window_sizes_share_left_edge_and_top()
    {
        var small = IslandPlacement.Compute(Primary.Bounds, 1.25, 452, 64, 420, 120, 6);
        var large = IslandPlacement.Compute(Primary.Bounds, 1.25, 452, 600, 420, 120, 6);
        Assert.Equal(small.Left, large.Left);
        Assert.Equal(small.Top, large.Top);
        Assert.Equal(small.Width, large.Width);
    }

    [Fact]
    public void Offset_is_clamped_so_the_panel_stays_on_the_monitor()
    {
        var far = IslandPlacement.Compute(Primary.Bounds, 1.0, 452, 600, 420, 4000, 0);
        int centre = far.Left + far.Width / 2;
        Assert.Equal(1920 - 210, centre);
        var left = IslandPlacement.Compute(Primary.Bounds, 1.0, 452, 600, 420, -4000, 0);
        Assert.Equal(210, left.Left + left.Width / 2);
    }

    [Fact]
    public void Placement_survives_a_bad_scale_and_a_tiny_monitor()
    {
        var r = IslandPlacement.Compute(new PixelRect(0, 0, 300, 200), 0, 452, 64, 420, 0, 0);
        Assert.Equal(452, r.Width);
        Assert.Equal(150, r.Left + r.Width / 2); // too small to clamp: plain centre
    }

    // ---- full screen (section 7.4) ----

    private static readonly PixelRect Mon = new(0, 0, 1920, 1080);

    [Fact]
    public void A_window_covering_the_island_monitor_is_full_screen()
    {
        Assert.True(FullScreenRules.IsFullScreen(NotificationState.AcceptsNotifications, Mon, "Chrome_WidgetWin_1",
            false, false, true, Mon));
        // Borders outside the monitor still cover it.
        Assert.True(FullScreenRules.IsFullScreen(NotificationState.Busy, new PixelRect(-8, -8, 1928, 1088), "VideoPlayer",
            false, false, true, Mon));
    }

    [Fact]
    public void A_normal_or_maximised_window_is_not_full_screen()
    {
        Assert.False(FullScreenRules.IsFullScreen(NotificationState.AcceptsNotifications, new PixelRect(0, 0, 1920, 1032),
            "Notepad", false, false, true, Mon));
        // Maximised with an auto-hiding taskbar covers the monitor, but it is not full screen.
        Assert.False(FullScreenRules.IsFullScreen(NotificationState.AcceptsNotifications, new PixelRect(-8, -8, 1928, 1088),
            "Notepad", false, true, true, Mon));
    }

    [Fact]
    public void Desktop_taskbar_and_our_own_window_never_count()
    {
        foreach (var cls in new[] { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
            Assert.False(FullScreenRules.IsFullScreen(NotificationState.Busy, Mon, cls, false, false, true, Mon));
        Assert.False(FullScreenRules.IsFullScreen(NotificationState.RunningD3DFullScreen, Mon, "HwndWrapper", true, false, true, Mon));
    }

    [Fact]
    public void Only_the_island_monitor_matters()
    {
        Assert.False(FullScreenRules.IsFullScreen(NotificationState.RunningD3DFullScreen, new PixelRect(1920, 0, 4480, 1440),
            "Game", false, false, onIslandMonitor: false, Mon));
    }

    [Fact]
    public void D3D_full_screen_and_presentation_mode_count_without_covering()
    {
        var small = new PixelRect(100, 100, 800, 600);
        Assert.True(FullScreenRules.IsFullScreen(NotificationState.RunningD3DFullScreen, small, "Game", false, false, true, Mon));
        Assert.True(FullScreenRules.IsFullScreen(NotificationState.PresentationMode, small, "PPTFrameClass", false, true, true, Mon));
        Assert.False(FullScreenRules.IsFullScreen(NotificationState.Busy, small, "Game", false, false, true, Mon));
    }

    // ---- reminder text and timer (section 7.9) ----

    private static readonly DateTime Now = new(2026, 11, 4, 15, 0, 0);

    private static DueReminder Due(long id, string title, long list, DateTime moment) =>
        new(Task(id, title, listId: list, due: moment, hasTime: true), moment);

    private static string? Names(long id) => id switch { 1 => "Personal", 2 => "Office Work", _ => null };

    [Fact]
    public void One_task_due_now_names_the_task_and_its_list()
    {
        var n = ReminderText.Describe([Due(5, "Call the bank", 2, Now)], late: false, Names)!;
        Assert.Equal("Office Work", n.Title);
        Assert.Equal("⏰ Due now: Call the bank", n.Text);
        Assert.Equal(2, n.ListId);
    }

    [Fact]
    public void Several_overdue_tasks_become_one_combined_notice()
    {
        var n = ReminderText.Describe([Due(1, "A", 1, Now.AddHours(-3)), Due(2, "B", 2, Now.AddHours(-1))], late: true, Names)!;
        Assert.Equal("TaskIsland", n.Title);
        Assert.Equal("⏰ 2 tasks overdue", n.Text);
        Assert.Null(n.ListId); // different lists → All lists
    }

    [Fact]
    public void A_batch_from_one_list_opens_that_list()
    {
        var n = ReminderText.Describe([Due(1, "A", 1, Now), Due(2, "B", 1, Now)], late: false, Names)!;
        Assert.Equal("⏰ 2 tasks due now", n.Text);
        Assert.Equal(1, n.ListId);
    }

    [Fact]
    public void Unknown_list_falls_back_to_the_app_name_and_empty_batch_gives_nothing()
    {
        Assert.Equal("TaskIsland", ReminderText.Describe([Due(1, "A", 99, Now)], false, Names)!.Title);
        Assert.Equal("⏰ Overdue: A", ReminderText.Describe([Due(1, "A", 99, Now)], true, Names)!.Text);
        Assert.Null(ReminderText.Describe([], false, Names));
    }

    [Fact]
    public void Late_means_more_than_a_minute_past()
    {
        Assert.False(ReminderText.IsLate(Due(1, "A", 1, Now.AddSeconds(-59)), Now));
        Assert.True(ReminderText.IsLate(Due(1, "A", 1, Now.AddSeconds(-61)), Now));
    }

    [Fact]
    public void Timer_wait_is_capped_at_24_hours_and_never_negative()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), ReminderText.Wait(Now.AddMinutes(2), Now));
        Assert.Equal(TimeSpan.FromHours(24), ReminderText.Wait(Now.AddDays(3), Now));
        Assert.Equal(TimeSpan.Zero, ReminderText.Wait(Now.AddMinutes(-5), Now));
    }
}
