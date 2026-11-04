using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using TaskIsland.Core.Services;

namespace TaskIsland.App.Controls;

/// <summary>One line in a settings card: title and description on the left, the control as content on the right.</summary>
public sealed class SettingRow : ContentControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingRow), new PropertyMetadata(""));
    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingRow), new PropertyMetadata(""));

    static SettingRow() => DefaultStyleKeyProperty.OverrideMetadata(typeof(SettingRow), new FrameworkPropertyMetadata(typeof(SettingRow)));

    public SettingRow() => Loaded += (_, _) => NameContent();

    /// <summary>
    /// Screen readers and UI tests find a control by its row title: the toggle, slider, drop-down, shortcut box or button in
    /// the row gets the title as its automation name (a segmented choice names each segment "Title: option").
    /// </summary>
    private void NameContent()
    {
        void Name(DependencyObject d)
        {
            if (string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(d)))
                System.Windows.Automation.AutomationProperties.SetName(d, Title);
        }
        switch (Content)
        {
            case SegmentedControl seg: seg.Label = Title; break;
            case Panel panel:
                foreach (var child in panel.Children.OfType<DependencyObject>().Where(c => c is Slider or ComboBox or CheckBox)) Name(child);
                break;
            case Button: break; // a button keeps its own text as its name
            case DependencyObject d: Name(d); break;
        }
    }

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
}

/// <summary>A row of mutually exclusive choices drawn as segments (Theme, Density, ...). Two-way <see cref="SelectedIndex"/>.</summary>
public sealed class SegmentedControl : ContentControl
{
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(
        nameof(Options), typeof(IReadOnlyList<string>), typeof(SegmentedControl),
        new PropertyMetadata(null, (d, _) => ((SegmentedControl)d).Rebuild()));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(SegmentedControl),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((SegmentedControl)d).Sync()));

    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal };
    private readonly string _group = "seg" + Guid.NewGuid().ToString("N");

    public SegmentedControl()
    {
        Focusable = false;
        Content = _panel;
        HorizontalAlignment = HorizontalAlignment.Right;
    }

    /// <summary>Set by the row: the title that prefixes each segment's automation name.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(SegmentedControl), new PropertyMetadata("", (d, _) => ((SegmentedControl)d).Rebuild()));

    public IReadOnlyList<string>? Options { get => (IReadOnlyList<string>?)GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    private void Rebuild()
    {
        _panel.Children.Clear();
        if (Options is null) return;
        for (int i = 0; i < Options.Count; i++)
        {
            int index = i;
            var b = new RadioButton { Content = Options[i], GroupName = _group, Style = (Style)FindResource("SettingsSegment") };
            if (Label.Length > 0) System.Windows.Automation.AutomationProperties.SetName(b, Label + ": " + Options[i]);
            b.Checked += (_, _) => SelectedIndex = index;
            _panel.Children.Add(b);
        }
        Sync();
    }

    private void Sync()
    {
        for (int i = 0; i < _panel.Children.Count; i++)
            if (_panel.Children[i] is RadioButton r) r.IsChecked = i == SelectedIndex;
    }
}

/// <summary>
/// Captures a keyboard shortcut (PLAN.md section 7.8): click, press the combination. Escape cancels. Only A–Z, 0–9, F1–F24
/// and Space are accepted as the key; the rule "at least two modifiers" and the in-use check belong to the owner, which
/// answers through <see cref="Validator"/> (null = accepted, else the message to show).
/// </summary>
public sealed class HotkeyBox : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(HotkeyBox), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty HintProperty = DependencyProperty.Register(
        nameof(Hint), typeof(string), typeof(HotkeyBox), new PropertyMetadata(""));
    public static readonly DependencyProperty CapturingProperty = DependencyProperty.Register(
        nameof(Capturing), typeof(bool), typeof(HotkeyBox), new PropertyMetadata(false));

    static HotkeyBox() => DefaultStyleKeyProperty.OverrideMetadata(typeof(HotkeyBox), new FrameworkPropertyMetadata(typeof(HotkeyBox)));

    public HotkeyBox() { Focusable = true; IsTabStop = true; }

    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new HotkeyBoxPeer(this);

    private sealed class HotkeyBoxPeer(HotkeyBox owner) : System.Windows.Automation.Peers.FrameworkElementAutomationPeer(owner)
    {
        protected override System.Windows.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() => System.Windows.Automation.Peers.AutomationControlType.Custom;
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
        protected override string GetClassNameCore() => "HotkeyBox";
        protected override string GetHelpTextCore() => owner.Text;
    }

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    /// <summary>A message under the box: "Press a shortcut…", or why the last one was refused.</summary>
    public string Hint { get => (string)GetValue(HintProperty); set => SetValue(HintProperty, value); }
    public bool Capturing { get => (bool)GetValue(CapturingProperty); private set => SetValue(CapturingProperty, value); }

    /// <summary>Called with the captured text such as "Ctrl+Alt+T". Returns null when it was applied, else the reason it was not.</summary>
    public Func<string, string?>? Validator { get; set; }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Focus();
        e.Handled = true;
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        Capturing = true;
        Hint = "Press the new shortcut (Esc to cancel)";
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        Capturing = false;
        if (Hint.StartsWith("Press the new", StringComparison.Ordinal)) Hint = "";
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!Capturing) { base.OnPreviewKeyDown(e); return; }
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { Keyboard.ClearFocus(); return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.None or Key.DeadCharProcessed or Key.ImeProcessed) return;

        var mods = HotkeyModifiers.None;
        var m = Keyboard.Modifiers;
        if (m.HasFlag(ModifierKeys.Control)) mods |= HotkeyModifiers.Control;
        if (m.HasFlag(ModifierKeys.Alt)) mods |= HotkeyModifiers.Alt;
        if (m.HasFlag(ModifierKeys.Shift)) mods |= HotkeyModifiers.Shift;
        if (m.HasFlag(ModifierKeys.Windows)) mods |= HotkeyModifiers.Win;

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        var gesture = new HotkeyGesture(mods, vk);
        string text = gesture.ToString();
        if (!HotkeyGesture.TryParse(text, out var parsed) || parsed != gesture)
        {
            Hint = "That key can't be used. Use a letter, a digit, F1–F24 or Space.";
            return;
        }
        if (!gesture.IsAllowedForRebind)
        {
            Hint = "Use at least two of Ctrl, Alt, Shift, Win with the key, so it doesn't clash with other apps.";
            return;
        }
        var problem = Validator?.Invoke(text);
        if (problem is null)
        {
            SetCurrentValue(TextProperty, text);
            Hint = "";
            Keyboard.ClearFocus();
        }
        else
        {
            Hint = problem;
        }
    }
}

/// <summary>True becomes false and back (enabling a section while something is busy).</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b ? !b : value;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b ? !b : value;
}
