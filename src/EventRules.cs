using System.Globalization;

namespace EventHorizon;

public static class EventRules
{
    public static DateTimeOffset Start(EventRecord item)
    {
        if (!DateTime.TryParseExact(item.StartLocal, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local)) throw new ArgumentException("Use yyyy-MM-dd HH:mm for the start.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(item.TimeZoneId);
        if (zone.IsInvalidTime(local)) throw new ArgumentException("That time is skipped by daylight saving time. Choose another time.");
        if (zone.IsAmbiguousTime(local)) throw new ArgumentException("That time occurs twice during daylight saving time. Choose an unambiguous time.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }

    public static string? Validate(EventRecord item, bool publish = false)
    {
        if (string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 100) return "Enter a title of 1–100 characters.";
        if (item.Description.Length > 1000) return "Discord descriptions can contain up to 1,000 characters.";
        if (item.Organizer.Length > 100 || item.Server.Length > 100 || item.SignupGroups.Length > 600)
            return "Organizer and server names must fit in 100 characters; signup groups must fit in 600 characters.";
        if (item.DurationMinutes is < 1 or > 10080) return "Choose a duration between 1 minute and 7 days.";
        try
        {
            var start = Start(item);
            if (publish && start <= DateTimeOffset.UtcNow) return "Publishing requires a future start time.";
        }
        catch (Exception ex) when (ex is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { return ex.Message; }
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
        try { return Start(item).AddMinutes(item.DurationMinutes) < DateTimeOffset.UtcNow; }
        catch { return false; }
    }
}
