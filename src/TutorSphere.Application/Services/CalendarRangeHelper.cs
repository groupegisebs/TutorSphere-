using TutorSphere.Application.DTOs.Calendar;

namespace TutorSphere.Application.Services;

public static class CalendarRangeHelper
{
    public static (DateTime Start, DateTime End) GetViewRange(CalendarView view, DateTime date)
    {
        var viewDate = date.Date;

        return view switch
        {
            CalendarView.Day => (viewDate, viewDate.AddDays(1)),
            CalendarView.Week => GetWeekRange(viewDate),
            CalendarView.Month => GetMonthRange(viewDate),
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, null)
        };
    }

    private static (DateTime Start, DateTime End) GetWeekRange(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        var start = date.AddDays(-diff);
        return (start, start.AddDays(7));
    }

    private static (DateTime Start, DateTime End) GetMonthRange(DateTime date)
    {
        var start = new DateTime(date.Year, date.Month, 1, 0, 0, 0, date.Kind);
        return (start, start.AddMonths(1));
    }
}
