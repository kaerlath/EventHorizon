namespace EventHorizon;

public static class EventCalendar
{
    public static DateTime WeekStart(DateTime day) => day.Date.AddDays(-(int)day.DayOfWeek);
    public static DateTime MonthStart(DateTime day) => WeekStart(new DateTime(day.Year, day.Month, 1));

    public static (DateTime Start, DateTime End)? Interval(EventRecord item, TimeZoneInfo zone)
    {
        try
        {
            var start = EventRules.Start(item);
            return (TimeZoneInfo.ConvertTime(start, zone).DateTime,
                TimeZoneInfo.ConvertTime(start.AddMinutes(item.DurationMinutes), zone).DateTime);
        }
        catch (Exception ex) when (ex is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException) { return null; }
    }

    public static EventRecord[] OnDay(IEnumerable<EventRecord> events, DateTime day, TimeZoneInfo zone) => events
        .Select(item => (Item: item, Times: Interval(item, zone)))
        .Where(x => x.Times is { } t && t.Start < day.Date.AddDays(1) && t.End > day.Date)
        .OrderBy(x => x.Times!.Value.Start).ThenBy(x => x.Item.Title).Select(x => x.Item).ToArray();
}
