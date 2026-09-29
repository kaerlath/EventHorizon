namespace EventHorizon;

// Curated from Square Enix announcements; these never enter the user's event store.
public static class OfficialEvents
{
    public const string VerifiedOn = "September 28, 2026";
    public static readonly EventRecord[] Items = [
        Entry("00000000-0000-0000-0000-000000000001", "Yo-kai Watch: Gather One, Gather All", "2026-08-04 08:00", "2026-10-05 14:59", "https://na.finalfantasyxiv.com/lodestone/topics/detail/5e9868fb47977cbd45effe2a50bdb78a2270430c", "SE-SE"),
        Entry("00000000-0000-0000-0000-000000000002", "Moogle Treasure Trove — The First Hunt for Astronomy", "2026-09-09 08:00", "2026-10-19 14:59", "https://na.finalfantasyxiv.com/lodestone/special/mogmog-collection/202609/lsekubsk45", "SE-RE"),
        Entry("00000000-0000-0000-0000-000000000003", "A Nocturne for Heroes", "2026-09-24 08:00", "2026-10-13 14:59", "https://na.finalfantasyxiv.com/lodestone/topics/detail/b7ff81b627294a36825255532371d6d8f9066d74", "SE-SE")
    ];
    private static EventRecord Entry(string id, string title, string start, string end, string url, string category) => new()
    {
        Id = Guid.Parse(id), Title = title, StartLocal = start, EndLocal = end, TimeZoneId = "UTC",
        ScheduleMode = "Continuous", Status = "Official · information only", ReadOnly = true, InformationOnly = true,
        Organizer = "Square Enix", SourceUrl = url,
        EventType = category,
        Description = "Official FINAL FANTASY XIV in-game event. Open the official announcement for participation requirements, rewards and any schedule changes. This calendar entry is for information only and is not sent to Discord or Google Calendar."
    };
}
