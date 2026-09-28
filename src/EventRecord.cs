namespace EventHorizon;

public sealed class EventRecord
{
    public bool ReadOnly { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public string World { get; set; } = "";
    public string StartLocal { get; set; } = "";
    public int DurationMinutes { get; set; } = 120;
    public string TimeZoneId { get; set; } = "";
    public string Organizer { get; set; } = "";
    public string BannerPath { get; set; } = "";
    public bool GoogleCalendarSync { get; set; }
    public bool RemoveBanner { get; set; }
    public string SignupsClose { get; set; } = "";
    public string Recurrence { get; set; } = "None";
    public string SignupGroups { get; set; } = "Guests:unlimited";
    public string Server { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public string GuildId { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public string DiscordEventId { get; set; } = "";
    public string DiscordMessageId { get; set; } = "";
    public string RelayOrigin { get; set; } = "";
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public AnnouncementStyle AnnouncementStyle { get; set; } = new();
    public EventRecord Copy()
    {
        var copy = (EventRecord)MemberwiseClone();
        copy.AnnouncementStyle = (AnnouncementStyle ?? new()).Copy();
        return copy;
    }
}
