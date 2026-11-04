using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace TaskIsland.App.Controls;

/// <summary>Month grid that raises <see cref="DateChosen"/> when a day is clicked. Weeks start on the culture's first day.</summary>
public partial class MiniCalendar : UserControl
{
    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(
        nameof(SelectedDate), typeof(DateTime?), typeof(MiniCalendar), new PropertyMetadata(null, (d, _) => ((MiniCalendar)d).Render()));

    private readonly Button[] _days = new Button[42];
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public MiniCalendar()
    {
        InitializeComponent();
        var fmt = CultureInfo.CurrentCulture.DateTimeFormat;
        for (int i = 0; i < 7; i++)
        {
            var day = (DayOfWeek)(((int)fmt.FirstDayOfWeek + i) % 7);
            WeekdayRow.Children.Add(new TextBlock
            {
                Text = fmt.GetShortestDayName(day),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2),
            });
            ((TextBlock)WeekdayRow.Children[i]).SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");
        }
        for (int i = 0; i < _days.Length; i++)
        {
            var b = new Button { Style = (Style)FindResource("DayButton") };
            b.Click += OnDayClick;
            _days[i] = b;
            DayGrid.Children.Add(b);
        }
        Render();
    }

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public event Action<DateTime>? DateChosen;

    /// <summary>Shows the month of the selected date (or today) before the popup opens.</summary>
    public void ShowSelectedMonth()
    {
        var d = SelectedDate ?? DateTime.Today;
        _month = new DateTime(d.Year, d.Month, 1);
        Render();
    }

    private void Render()
    {
        MonthText.Text = _month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        int first = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        int offset = ((int)_month.DayOfWeek - first + 7) % 7;
        var start = _month.AddDays(-offset);
        var today = DateTime.Today;
        for (int i = 0; i < _days.Length; i++)
        {
            var date = start.AddDays(i);
            var b = _days[i];
            b.Content = date.Day.ToString(CultureInfo.CurrentCulture);
            b.DataContext = date;
            b.Tag = SelectedDate?.Date == date ? "selected" : date == today ? "today" : date.Month != _month.Month ? "other" : "";
            Automation(b, date);
        }
    }

    private static void Automation(Button b, DateTime date) =>
        System.Windows.Automation.AutomationProperties.SetName(b, date.ToString("d MMMM yyyy", CultureInfo.CurrentCulture));

    private void OnDayClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is DateTime date) DateChosen?.Invoke(date);
    }

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        _month = _month.AddMonths(-1);
        Render();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _month = _month.AddMonths(1);
        Render();
    }
}
