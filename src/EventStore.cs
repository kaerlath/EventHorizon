using System.Text.Json;

namespace EventHorizon;

public sealed class EventStore
{
    private readonly string path;
    private readonly JsonSerializerOptions options = new() { WriteIndented = true };
    public List<EventRecord> Events { get; private set; } = new();
    public List<EventRecord> Templates { get; private set; } = new();

    public EventStore(string directory)
    {
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, "events.json");
        if (!File.Exists(path)) return;
        try
        {
            var data = JsonSerializer.Deserialize<StoreData>(File.ReadAllText(path));
            Events = data?.Events ?? new();
            Templates = data?.Templates ?? new();
            Appearance = data?.Appearance ?? new();
            Reminders = data?.Reminders ?? new();
            ShowWelcomeHelp = data?.ShowWelcomeHelp ?? true;
            HelpStep = data?.HelpStep ?? 0;
            RelayUrl = data?.RelayUrl ?? "";
            RelayAddressLocked = data?.RelayAddressLocked ?? true;
            GuildId = data?.GuildId ?? "";
            GuildName = data?.GuildName ?? "";
            ChannelId = data?.ChannelId ?? "";
            ChannelName = data?.ChannelName ?? "";
        }
        catch (Exception ex)
        {
            // Keep the damaged data available for recovery; never overwrite it silently.
            LoadError = ex.Message;
        }
    }

    public AppearanceSettings Appearance { get; set; } = new();
    public bool ShowWelcomeHelp { get; set; } = true;
    public int HelpStep { get; set; }
    public ReminderSettings Reminders { get; private set; } = new();
    public void ChangeReminders(Action<ReminderSettings> change)
    {
        var before = Reminders.Copy();
        try { change(Reminders); Save(); }
        catch { Reminders = before; throw; }
    }
    public string? LoadError { get; }
    public string RelayUrl { get; set; } = "";
    public bool RelayAddressLocked { get; set; } = true;
    public string GuildId { get; set; } = "";
    public string GuildName { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public string ChannelName { get; set; } = "";
    public void Upsert(EventRecord item, bool template = false)
    {
        var target = template ? Templates : Events;
        var before = target.Select(x => x.Copy()).ToList();
        var index = target.FindIndex(x => x.Id == item.Id);
        if (index < 0) target.Add(item.Copy()); else target[index] = item.Copy();
        try { Save(); }
        catch { target.Clear(); target.AddRange(before); throw; }
    }
    public void Save()
    {
        if (LoadError is not null) throw new InvalidOperationException("Existing event data could not be read: " + LoadError);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new StoreData(Events, Templates, RelayUrl, GuildId, GuildName, ChannelId, ChannelName, RelayAddressLocked, Appearance, Reminders, ShowWelcomeHelp, HelpStep), options));
        File.Move(temp, path, true);
    }

    private sealed record StoreData(List<EventRecord> Events, List<EventRecord> Templates, string RelayUrl,
        string GuildId = "", string GuildName = "", string ChannelId = "", string ChannelName = "", bool RelayAddressLocked = true, AppearanceSettings? Appearance = null, ReminderSettings? Reminders = null, bool ShowWelcomeHelp = true, int HelpStep = 0);
}
