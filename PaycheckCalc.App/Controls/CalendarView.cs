using System.Globalization;

namespace PaycheckCalc.App.Controls;

/// <summary>
/// A self-contained inline month calendar: a header with previous/next month
/// arrows, a day-of-week row, and a tappable grid of days. Lets the user scroll
/// through months and pick a day, exposed as the bindable <see cref="SelectedDate"/>.
/// Used to capture the pay date that anchors the pay-period schedule.
/// </summary>
public sealed class CalendarView : ContentView
{
    private static readonly Color AccentColor = Color.FromArgb("#333333");
    private static readonly Color SurfaceColor = Color.FromArgb("#F2F2F2");
    private static readonly Color MutedColor = Color.FromArgb("#BDBDBD");
    private static readonly Color TextColor = Color.FromArgb("#424242");

    private readonly Label _monthLabel;
    private readonly Grid _daysGrid;

    // First day of the month currently displayed in the grid.
    private DateOnly _displayMonth = FirstOfMonth(DateOnly.FromDateTime(DateTime.Today));

    public CalendarView()
    {
        _monthLabel = new Label
        {
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor,
            FontSize = 14
        };

        var prev = new Button
        {
            Text = "◀", // ◀
            BackgroundColor = Colors.Transparent,
            TextColor = AccentColor,
            FontSize = 16,
            Padding = 0,
            WidthRequest = 44
        };
        prev.Clicked += (_, _) => ShiftMonth(-1);

        var next = new Button
        {
            Text = "▶", // ▶
            BackgroundColor = Colors.Transparent,
            TextColor = AccentColor,
            FontSize = 16,
            Padding = 0,
            WidthRequest = 44
        };
        next.Clicked += (_, _) => ShiftMonth(1);

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        header.Add(prev, 0, 0);
        header.Add(_monthLabel, 1, 0);
        header.Add(next, 2, 0);

        var weekdayRow = BuildWeekdayRow();

        _daysGrid = new Grid
        {
            ColumnSpacing = 2,
            RowSpacing = 2,
            ColumnDefinitions = MakeSevenColumns(),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };

        Content = new Border
        {
            BackgroundColor = SurfaceColor,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Padding = 8,
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children = { header, weekdayRow, _daysGrid }
            }
        };

        RebuildCalendar();
    }

    /// <summary>The day the user selected (defaults to today).</summary>
    public static readonly BindableProperty SelectedDateProperty = BindableProperty.Create(
        nameof(SelectedDate),
        typeof(DateOnly),
        typeof(CalendarView),
        defaultValue: DateOnly.FromDateTime(DateTime.Today),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnSelectedDateChanged);

    public DateOnly SelectedDate
    {
        get => (DateOnly)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    private static void OnSelectedDateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (CalendarView)bindable;
        // Page the grid to the selected day's month, then refresh highlighting.
        view._displayMonth = FirstOfMonth((DateOnly)newValue);
        view.RebuildCalendar();
    }

    private void ShiftMonth(int delta)
    {
        _displayMonth = _displayMonth.AddMonths(delta);
        RebuildCalendar();
    }

    private void RebuildCalendar()
    {
        _monthLabel.Text = _displayMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        _daysGrid.Children.Clear();

        int daysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);
        // Sunday = column 0.
        int leadingBlanks = (int)new DateTime(_displayMonth.Year, _displayMonth.Month, 1).DayOfWeek;

        for (int day = 1; day <= daysInMonth; day++)
        {
            int cellIndex = leadingBlanks + (day - 1);
            int row = cellIndex / 7;
            int col = cellIndex % 7;

            var date = new DateOnly(_displayMonth.Year, _displayMonth.Month, day);
            bool isSelected = date == SelectedDate;

            var dayButton = new Button
            {
                Text = day.ToString(CultureInfo.CurrentCulture),
                FontSize = 13,
                Padding = 0,
                HeightRequest = 40,
                CornerRadius = 6,
                BackgroundColor = isSelected ? AccentColor : Colors.White,
                TextColor = isSelected ? Colors.White : TextColor
            };
            var captured = date;
            dayButton.Clicked += (_, _) => SelectedDate = captured;

            _daysGrid.Add(dayButton, col, row);
        }
    }

    private static Grid BuildWeekdayRow()
    {
        var row = new Grid { ColumnSpacing = 2, ColumnDefinitions = MakeSevenColumns() };
        string[] names = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        for (int i = 0; i < names.Length; i++)
        {
            row.Add(new Label
            {
                Text = names[i],
                HorizontalTextAlignment = TextAlignment.Center,
                FontSize = 11,
                TextColor = MutedColor
            }, i, 0);
        }
        return row;
    }

    private static ColumnDefinitionCollection MakeSevenColumns()
    {
        var cols = new ColumnDefinitionCollection();
        for (int i = 0; i < 7; i++)
            cols.Add(new ColumnDefinition(GridLength.Star));
        return cols;
    }

    private static DateOnly FirstOfMonth(DateOnly d) => new(d.Year, d.Month, 1);
}
