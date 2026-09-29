namespace EventHorizon;

public static class CalendarLanes
{
    // Reuse a lane only if the events occupy disjoint days. Keep a range in one
    // lane across the whole week, including its empty session days.
    public static EventRecord[][] Build(IEnumerable<EventRecord> events, DateTime week, TimeZoneInfo zone)
    {
        var lanes = new List<List<EventRecord>>();
        var masks = new List<int>();
        foreach (var item in events)
        {
            var mask = 0;
            for (var day=0; day<7; day++) if(EventCalendar.OccursOnDay(item,week.AddDays(day),zone)) mask |= 1 << day;
            if(mask == 0) continue;
            var lane = masks.FindIndex(existing => (existing & mask) == 0);
            if(lane < 0) { lanes.Add([]); masks.Add(0); lane=lanes.Count-1; }
            lanes[lane].Add(item); masks[lane] |= mask;
        }
        return lanes.Select(l => l.ToArray()).ToArray();
    }
    public static bool CrossesMidnight(EventRecord item, DateTime midnight, TimeZoneInfo zone) => EventRules.Intervals(item).Any(t =>
        TimeZoneInfo.ConvertTime(t.Start,zone).DateTime < midnight && TimeZoneInfo.ConvertTime(t.End,zone).DateTime > midnight);
}
