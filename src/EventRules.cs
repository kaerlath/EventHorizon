using System.Globalization;

namespace EventHorizon;

public static class EventRules
{
    public static string Itinerary(EventRecord item) => item.ScheduleMode != "Sessions" ? "" :
        "Session schedule (" + item.TimeZoneId + "):\n" + string.Join("\n", item.Sessions.OrderBy(s => s.StartLocal).Select(s => s.StartLocal + " – " + s.EndLocal));
    public static string DiscordDescription(EventRecord item)
    {
        var plan = Itinerary(item);
        if (plan.Length == 0) return item.Description;
        var room = Math.Max(0, 1000 - plan.Length - 2);
        var text = item.Description.Length > room ? item.Description[..Math.Max(0, room - 48)] + "… Full description in the announcement." : item.Description;
        return text + "\n\n" + plan;
    }
    public static DateTimeOffset Start(EventRecord item)
        => Intervals(item)[0].Start;
    public static DateTimeOffset End(EventRecord item) => Intervals(item)[^1].End;
    public static (DateTimeOffset Start, DateTimeOffset End)[] Intervals(EventRecord item)
    {
        var rows = item.ScheduleMode switch {
            "Single" => new[] { (ParseTime(item.StartLocal, item.TimeZoneId), ParseTime(item.StartLocal, item.TimeZoneId).AddMinutes(item.DurationMinutes)) },
            "Continuous" => new[] { (ParseTime(item.StartLocal, item.TimeZoneId), ParseTime(item.EndLocal, item.TimeZoneId)) },
            "Sessions" when item.Sessions is { Count: >= 1 and <= 12 } => item.Sessions.Select(s => (ParseTime(s.StartLocal, item.TimeZoneId), ParseTime(s.EndLocal, item.TimeZoneId))).OrderBy(s => s.Item1).ToArray(),
            _ => throw new ArgumentException("Choose a schedule with 1–12 sessions.")
        };
        for (var i = 0; i < rows.Length; i++)
        {
            if (rows[i].Item2 <= rows[i].Item1) throw new ArgumentException("Each end must be after its start.");
            if (i > 0 && rows[i].Item1 < rows[i - 1].Item2) throw new ArgumentException("Sessions must not overlap.");
        }
        if (rows[^1].Item2 - rows[0].Item1 > TimeSpan.FromDays(366)) throw new ArgumentException("An event can span at most 366 days.");
        return rows;
    }
    public static DateTimeOffset ParseTime(string value, string timeZoneId)
    {
        if (!DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local)) throw new ArgumentException("Use yyyy-MM-dd HH:mm for the start.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        if (zone.IsInvalidTime(local)) throw new ArgumentException("That time is skipped by daylight saving time. Choose another time.");
        if (zone.IsAmbiguousTime(local)) throw new ArgumentException("That time occurs twice during daylight saving time. Choose an unambiguous time.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }

    public static string? Validate(EventRecord item, bool publish = false)
    {
        if (item.InformationOnly) return "Official events are information only.";
        if (item.Tags.Length > 250) return "Keep tags within 250 characters.";
        if (item.EventType.Length > 0 && !EventTypes.All.Any(c => c.Code == item.EventType)) return "Choose a valid event type.";
        item.EventType = EventTypes.For(item).Code;
        if (string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 100) return "Enter a title of 1–100 characters.";
        if (item.Description.Length > 1000) return "Discord descriptions can contain up to 1,000 characters.";
        if (item.Organizer.Length > 100 || item.Server.Length > 100 || item.SignupGroups.Length > 600)
            return "Organizer and server names must fit in 100 characters; signup groups must fit in 600 characters.";
        if (item.ScheduleMode == "Single" && (item.DurationMinutes is < 1 or > 527040)) return "Choose a duration between 1 minute and 366 days.";
        try
        {
            var start = Start(item);
            if (publish && start <= DateTimeOffset.UtcNow) return "Publishing requires a future start time.";
            if (item.ScheduleMode != "Single")
            {
                item.StartLocal = TimeZoneInfo.ConvertTime(start, TimeZoneInfo.FindSystemTimeZoneById(item.TimeZoneId)).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                item.DurationMinutes = (int)(End(item) - start).TotalMinutes;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { return ex.Message; }
        if (publish && item.PersonalOnly) return "Personal events cannot be published to Discord.";
        if (publish)
        {
            if (!ulong.TryParse(item.GuildId, out _)) return "Select a Discord server in Settings.";
            if (string.IsNullOrWhiteSpace(item.Location) || (item.World + " — " + item.Location).Length > 100)
                return "Enter a location; world and location together must fit in 100 characters.";
            if (item.Recurrence != "None") return "Recurring publication is not available yet. Publish a single event or keep this as a recurring draft.";
        }
        return null;
    }

    public static bool HasEnded(EventRecord item)
    {
        try { return End(item) < DateTimeOffset.UtcNow; }
        catch { return false; }
    }
}
