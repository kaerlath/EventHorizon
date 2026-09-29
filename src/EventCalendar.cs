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
                TimeZoneInfo.ConvertTime(EventRules.End(item), zone).DateTime);
        }
        catch (Exception ex) when (ex is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException) { return null; }
    }

    public static EventRecord[] OnDay(IEnumerable<EventRecord> events, DateTime day, TimeZoneInfo zone) => events
        .Select(item => (Item: item, Times: Interval(item, zone)))
        .Where(x => x.Times is not null && OccursOnDay(x.Item, day, zone))
        .OrderBy(x => x.Times!.Value.Start).ThenBy(x => x.Item.Title).Select(x => x.Item).ToArray();
    public static bool OccursOnDay(EventRecord item, DateTime day, TimeZoneInfo zone)
    {
        try { return EventRules.Intervals(item).Any(t => TimeZoneInfo.ConvertTime(t.Start, zone).DateTime < day.Date.AddDays(1) && TimeZoneInfo.ConvertTime(t.End, zone).DateTime > day.Date); }
        catch (Exception ex) when (ex is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException) { return false; }
    }
}
