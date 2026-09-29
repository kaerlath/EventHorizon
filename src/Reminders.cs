namespace EventHorizon;

public sealed class ReminderSettings
{
    public bool Enabled { get; set; }
    public List<EventReminder> Items { get; set; } = [];
    public ReminderSettings Copy() => new() { Enabled = Enabled, Items = Items.Select(x => x.Copy()).ToList() };
}

public sealed class EventReminder
{
    public Guid EventId { get; set; }
    public string RelayOrigin { get; set; } = "";
    public string Title { get; set; } = "";
    public string Location { get; set; } = "";
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public int MinutesBefore { get; set; } = 15;
    public DateTimeOffset? DismissedStartUtc { get; set; }
    public DateTimeOffset? SnoozedUntilUtc { get; set; }
    public EventReminder Copy() => (EventReminder)MemberwiseClone();
    public void Refresh(EventRecord item, DateTimeOffset? now = null)
    {
        var intervals = EventRules.Intervals(item);
        var next = intervals.FirstOrDefault(t => t.Start > (now ?? DateTimeOffset.UtcNow).AddMinutes(-10));
        if (next == default) next = intervals[^1];
        var start = next.Start;
        if (start != StartUtc) { DismissedStartUtc = null; SnoozedUntilUtc = null; }
        StartUtc = start; EndUtc = next.End;
        Title = item.Title; Location = string.Join(" — ", new[] { item.World, item.Location }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}

public static class ReminderClock
{
    public static EventReminder[] Due(ReminderSettings settings, DateTimeOffset now) => !settings.Enabled ? [] : settings.Items
        .Where(r => r.DismissedStartUtc != r.StartUtc && (r.SnoozedUntilUtc is null || r.SnoozedUntilUtc <= now)
            && (r.StartUtc - now).TotalMinutes <= Math.Clamp(r.MinutesBefore, 0, 1440)
            && (r.StartUtc - now).TotalMinutes > -10 && now < r.EndUtc)
        .OrderBy(r => r.StartUtc).ToArray();

    public static string Countdown(DateTimeOffset start, DateTimeOffset now)
    {
        var remaining = start - now;
        if (remaining <= TimeSpan.Zero) return "The event has started";
        return remaining.TotalHours >= 1 ? $"Starts in {(int)remaining.TotalHours}h {remaining.Minutes:00}m"
            : $"Starts in {remaining.Minutes:00}:{remaining.Seconds:00}";
    }
}
