using System.Net;
using System.Text.Json;
using EventHorizon;
using Microsoft.Extensions.Configuration;

var root = Path.Combine(Path.GetTempPath(), "eventhorizon-checks-" + Guid.NewGuid());
Directory.CreateDirectory(root);
int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
async Task Reject(Func<Task> action, string name) { try { await action(); } catch { Check(true, name); return; } throw new Exception(name); }
EventRecord Make() => new() { Title = "Moonlit gathering", Description = "A community evening", StartLocal = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd HH:mm"), TimeZoneId = "UTC", Location = "Lavender Beds", World = "Balmung", GuildId = "123", ChannelId = "456" };
Check(CalendarText.Wrap("Autumnal Masquerade", 10, s => s.Length).SequenceEqual(new[] { "Autumnal", "Masquerade" }), "calendar wraps words");
Check(CalendarText.Wrap("Autumnal Masquerade", 30, s => s.Length).Length == 1, "wide calendar title stays single line");
Check(CalendarText.Wrap("abcdefghij", 4, s => s.Length).SequenceEqual(new[] { "abcd", "efgh", "ij" }), "long calendar words wrap");
Check(CalendarText.Wrap("A\nB", 9, s => s.Length).SequenceEqual(new[] { "A", "B" }), "calendar newlines survive");
Check(CalendarText.Wrap("\U0001F600\U0001F600", 1, s => s.Length).SequenceEqual(new[] { "\U0001F600", "\U0001F600" }), "calendar Unicode survives");
var sample = Make();
var privateEvent = Make(); privateEvent.PersonalOnly = true; privateEvent.GuildId = ""; privateEvent.ChannelId = ""; privateEvent.Location = ""; privateEvent.Status = "Personal";
Check(EventRules.Validate(privateEvent) is null, "personal event needs no Discord destination or location");
Check(EventRules.Validate(privateEvent, true) is not null, "personal event cannot publish to Discord");
var privateStore = new EventStore(Path.Combine(root, "private")); privateStore.Upsert(privateEvent);
var privateReload = new EventStore(Path.Combine(root, "private")).Events.Single();
Check(privateReload.PersonalOnly && privateReload.Status == "Personal", "personal visibility survives local saving");
Check(EventCalendar.OnDay(new[] { privateReload }, EventRules.Start(privateReload).UtcDateTime.Date, TimeZoneInfo.Utc).Length == 1, "personal event appears on its calendar day");
var privateReminder = new EventReminder { EventId = privateReload.Id, MinutesBefore = 15 }; privateReminder.Refresh(privateReload);
Check(ReminderClock.Due(new ReminderSettings { Enabled = true, Items = new() { privateReminder } }, EventRules.Start(privateReload).AddMinutes(-10)).Length == 1, "personal event supports countdown reminders");

var reminderNow = new DateTimeOffset(2035, 6, 12, 17, 45, 0, TimeSpan.Zero);
var reminderEvent = Make(); reminderEvent.StartLocal = "2035-06-12 18:00";
var personalReminder = new EventReminder { EventId = reminderEvent.Id, MinutesBefore = 15 };
personalReminder.Refresh(reminderEvent);
var reminderSettings = new ReminderSettings { Items = [personalReminder] };
Check(ReminderClock.Due(reminderSettings, reminderNow).Length == 0, "reminders default off");
reminderSettings.Enabled = true;
Check(ReminderClock.Due(reminderSettings, reminderNow.AddSeconds(-1)).Length == 0, "reminder does not fire before threshold");
Check(ReminderClock.Due(reminderSettings, reminderNow).Length == 1, "reminder fires at configured threshold independently of window state");
personalReminder.SnoozedUntilUtc = reminderNow.AddMinutes(5);
Check(ReminderClock.Due(reminderSettings, reminderNow).Length == 0 && ReminderClock.Due(reminderSettings, reminderNow.AddMinutes(5)).Length == 1, "snooze suppresses until due again");
personalReminder.DismissedStartUtc = personalReminder.StartUtc;
Check(ReminderClock.Due(reminderSettings, reminderNow.AddMinutes(5)).Length == 0, "dismiss suppresses the same occurrence");
var reminderFolder = Path.Combine(root, "reminders");
var reminderStore = new EventStore(reminderFolder);
reminderStore.ChangeReminders(s => { s.Enabled = true; s.Items.Add(personalReminder.Copy()); });
var restoredReminders = new EventStore(reminderFolder);
Check(restoredReminders.Reminders.Enabled && restoredReminders.Reminders.Items[0].DismissedStartUtc == personalReminder.StartUtc, "reminder choice and dismissal survive reload");
reminderEvent.StartLocal = "2035-06-12 18:30"; personalReminder.Refresh(reminderEvent);
Check(personalReminder.DismissedStartUtc is null && personalReminder.SnoozedUntilUtc is null, "rescheduling resets dismissal and snooze");
Check(ReminderClock.Due(reminderSettings, personalReminder.StartUtc.AddMinutes(10)).Length == 0, "old events cannot create stale popups");
Check(ReminderClock.Countdown(personalReminder.StartUtc, personalReminder.StartUtc.AddSeconds(-65)) == "Starts in 01:05", "countdown shows remaining minutes and seconds");
Directory.CreateDirectory(Path.Combine(reminderFolder, "events.json.tmp"));
await Reject(() => { restoredReminders.ChangeReminders(s => s.Enabled = false); return Task.CompletedTask; }, "reminder save failure reported");
Check(restoredReminders.Reminders.Enabled, "reminder save failure rolls back preferences");
Check(EventRules.Validate(sample, true) is null, "valid publish sample");
sample.StartLocal = "2026-03-08 02:30"; sample.TimeZoneId = "Mountain Standard Time";
Check(EventRules.Validate(sample) is not null, "daylight saving skipped time rejected");
sample.StartLocal = "2026-11-01 01:30";
Check(EventRules.Validate(sample) is not null, "daylight saving ambiguous time rejected");
sample = Make(); sample.Recurrence = "Weekly";
Check(EventRules.Validate(sample, true) is not null && EventRules.Validate(sample) is null, "recurrence planning cannot silently publish once");
var cal = Make(); cal.StartLocal = "2026-09-28 23:00"; cal.DurationMinutes = 120;
Check(EventCalendar.OnDay([cal], new DateTime(2026, 9, 29), TimeZoneInfo.Utc).Length == 1, "calendar includes overnight continuation");
cal.DurationMinutes = 60;
Check(EventCalendar.OnDay([cal], new DateTime(2026, 9, 29), TimeZoneInfo.Utc).Length == 0, "midnight end does not occupy following day");
Check(EventCalendar.WeekStart(new DateTime(2026, 10, 1)) == new DateTime(2026, 9, 27), "week crosses month boundary");
Check(EventCalendar.MonthStart(new DateTime(2028, 2, 29)) == new DateTime(2028, 1, 30), "leap month grid starts on Sunday");
cal.StartLocal = "2026-09-29 02:00";
Check(EventCalendar.OnDay([cal], new DateTime(2026, 9, 28), TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time")).Length == 1, "calendar converts events to viewer timezone");
cal.StartLocal = "invalid";
Check(EventCalendar.OnDay([cal], DateTime.Today, TimeZoneInfo.Utc).Length == 0, "invalid legacy dates do not break calendar");
var local = Path.Combine(root, "local"); Directory.CreateDirectory(local);
File.WriteAllText(Path.Combine(local, "events.json"), "{\"Events\":[{\"Title\":\"old draft\"}],\"Templates\":[],\"RelayUrl\":\"\"}");
var store = new EventStore(local);
Check(store.LoadError is null && store.Events[0].GuildId == "" && store.Events[0].Title == "old draft", "old data migrates without losing fields");
Check(store.ShowWelcomeHelp && store.HelpStep == 0, "existing saves receive welcome guide defaults");
var helpStore = new EventStore(Path.Combine(root, "help")) { ShowWelcomeHelp = false, HelpStep = 5 };
helpStore.Save();
var restoredHelp = new EventStore(Path.Combine(root, "help"));
Check(!restoredHelp.ShowWelcomeHelp && restoredHelp.HelpStep == 5, "walkthrough position and skip preference survive reload");
Directory.CreateDirectory(Path.Combine(local, "events.json.tmp"));
await Reject(() => { store.Upsert(Make()); return Task.CompletedTask; }, "failed save reported");
Check(store.Events.Count == 1, "failed save rolls back in-memory change");
var appearanceFolder = Path.Combine(root, "appearance");
var appearanceStore = new EventStore(appearanceFolder);
appearanceStore.Appearance.Theme = "Aurora"; appearanceStore.Appearance.ReduceMotion = true;
appearanceStore.Appearance.BackgroundOpacity = .45f;
appearanceStore.Appearance.CalendarFontScale = .8f;
appearanceStore.Appearance.CalendarTextColor = "#FF0000";
appearanceStore.Appearance.CalendarEventStyles["test-event"] = new() { Scale = .7f, Color = "#CC0000" };
var styled = Make(); styled.GoogleCalendarSync = true; styled.AnnouncementStyle.Title.Outline = 2;
styled.AnnouncementStyle.Paragraphs[1] = new() { Italic = true, Size = 30 };
appearanceStore.Upsert(styled);
var loadedAppearance = new EventStore(appearanceFolder);
Check(loadedAppearance.Appearance.CalendarFontScale == .8f && loadedAppearance.Appearance.CalendarTextColor == "#FF0000" && loadedAppearance.Appearance.CalendarEventStyles["test-event"].Scale == .7f, "calendar preferences persist");
Check(store.Appearance.CalendarFontScale == 1 && store.Appearance.CalendarEventStyles.Count == 0, "legacy calendar defaults");
Check(loadedAppearance.Appearance.Theme == "Aurora" && loadedAppearance.Appearance.ReduceMotion, "appearance preferences persist");
Check(loadedAppearance.Appearance.BackgroundOpacity == .45f && store.Appearance.BackgroundOpacity == 1, "opacity persists and existing settings default to opaque");
Check(loadedAppearance.Events[0].GoogleCalendarSync && !store.Events[0].GoogleCalendarSync, "Google sync opt-in persists with legacy default off");
Check(loadedAppearance.Events[0].AnnouncementStyle.Title.Outline == 2 && loadedAppearance.Events[0].AnnouncementStyle.Paragraphs[1].Italic, "event typography persists");
var cloneStyle = styled.Copy(); cloneStyle.AnnouncementStyle.Title.Outline = 0; cloneStyle.AnnouncementStyle.Paragraphs[1].Italic = false;
Check(styled.AnnouncementStyle.Title.Outline == 2 && styled.AnnouncementStyle.Paragraphs[1].Italic, "discarded typography edits cannot mutate saved event");
Check(store.Appearance.Theme == "Event Horizon" && store.Events[0].AnnouncementStyle.Body.Size == 22, "legacy saves receive appearance defaults");
var damaged = Path.Combine(root, "damaged"); Directory.CreateDirectory(damaged);
File.WriteAllText(Path.Combine(damaged, "events.json"), "broken");
var damagedStore = new EventStore(damaged);
await Reject(() => { damagedStore.Save(); return Task.CompletedTask; }, "damaged data cannot be overwritten");
Check(File.ReadAllText(Path.Combine(damaged, "events.json")) == "broken", "damaged data preserved");

var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{ ["DISCORD_CLIENT_ID"] = "test", ["DISCORD_CLIENT_SECRET"] = "test", ["DISCORD_BOT_TOKEN"] = "test", ["PUBLIC_ORIGIN"] = "http://localhost:5187", ["EVENT_DATA_PATH"] = Path.Combine(root, "relay.json") }).Build();
var fake = new FakeDiscord(); var relay = new Relay(config, fake);
var login = new Login("42", "Tester", "test-user", DateTimeOffset.UtcNow.AddHours(1));
Check((await relay.Guilds(login)).Length == 1, "eligible server returned");
Check((await relay.Channels(login, "123")).Length == 1, "channel allowed for user and bot");
fake.DenyChannel = true;
Check((await relay.Channels(login, "123")).Length == 0, "member overwrite denies channel");
await Reject(async () => { await relay.Publish(login, Make().Id, Make()); }, "mismatched id rejected");
sample = Make();
await Reject(async () => { await relay.Publish(login, sample.Id, sample); }, "publishing denied channel blocked");
fake.DenyChannel = false;
var result = JsonSerializer.SerializeToElement(await relay.Publish(login, sample.Id, sample));
Check(result.GetProperty("eventId").GetString() == "777" && fake.Creates == 1 && fake.Posts == 1, "creates event and announcement");
sample.Title = "Updated gathering";
await relay.Publish(login, sample.Id, sample);
Check(fake.Creates == 1 && fake.Posts == 1 && fake.Edits == 1 && fake.MessageEdits == 1, "retry edits existing Discord objects");
var history = await relay.History(login);
Check(history.Length == 1 && history[0].Title == sample.Title && history[0].DiscordEventId == "777", "published history recoverable");
Check((await relay.History(login with { UserId = "43" })).Length == 0, "history isolated by account");
await Reject(async () => { await relay.Publish(login with { UserId = "43" }, sample.Id, sample); }, "other account cannot reuse publication id");
sample.ChannelId = "";
await Reject(async () => { await relay.Publish(login, sample.Id, sample); }, "published destination immutable");
sample = Make(); fake.FailCreate = true;
await Reject(async () => { await relay.Publish(login, sample.Id, sample); }, "uncertain create reported");
var calls = fake.Creates;
fake.FailCreate = false;
await Reject(async () => { await relay.Publish(login, sample.Id, sample); }, "uncertain create cannot duplicate on retry");
Check(calls == fake.Creates, "retry did not call Discord create");
var restarted = new Relay(config, fake);
await Reject(async () => { await restarted.Publish(login, sample.Id, sample); }, "uncertain create survives relay restart");
fake.DenyGuild = true; var denied = Make();
await Reject(async () => { await relay.Publish(login, denied.Id, denied); }, "revoked guild permission enforced");
fake.DenyGuild = false;
var partial = Make(); fake.FailMessage = true;
var partialResult = JsonSerializer.SerializeToElement(await relay.Publish(login, partial.Id, partial));
Check(partialResult.GetProperty("eventId").GetString() == "777" && partialResult.GetProperty("warning").GetString() is not null, "partial announcement failure preserves event result");
var postCalls = fake.Posts; fake.FailMessage = false;
await relay.Publish(login, partial.Id, partial);
Check(fake.Posts == postCalls, "uncertain announcement is not duplicated");
var start = JsonSerializer.SerializeToElement(relay.StartLink());
var poll = start.GetProperty("PollToken").GetString()!;
var key = new Uri(start.GetProperty("verificationUrl").GetString()!).Segments.Last();
Check(JsonSerializer.SerializeToElement(relay.Poll(poll)).GetProperty("status").GetString() == "pending", "link starts pending");
var auth = new Uri(relay.Authorization(key));
var state = auth.Query.Split('&').Single(x => x.StartsWith("state="))[6..];
await relay.Callback("test-code", state);
var connected = JsonSerializer.SerializeToElement(relay.Poll(poll));
Check(connected.GetProperty("status").GetString() == "connected" && connected.GetProperty("token").GetString()!.Length == 64, "OAuth link returns random session");
await Reject(() => { relay.Poll(poll); return Task.CompletedTask; }, "pairing token single use");
await Reject(() => relay.Callback("test-code", state), "OAuth state single use");
var imageBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jT1kAAAAASUVORK5CYII=");
var imageData = BannerUpload.Encode(imageBytes);
Check(BannerUpload.Decode(imageData)!.Value.Bytes.SequenceEqual(imageBytes), "banner bytes round-trip intact");
await Reject(() => { BannerUpload.Encode(new byte[BannerUpload.MaxBytes + 1]); return Task.CompletedTask; }, "oversized banner rejected");
await Reject(() => { BannerUpload.Decode(imageData.Replace("image/png", "image/jpeg")); return Task.CompletedTask; }, "mislabeled image rejected");
await Reject(() => { BannerUpload.Decode("data:image/png;base64,not-image"); return Task.CompletedTask; }, "invalid image encoding rejected");
var illustrated = Make(); illustrated.Organizer = "Kaerl";
await relay.Publish(login, illustrated.Id, illustrated, imageData);
Check(fake.LastEvent.GetProperty("image").GetString() == imageData, "event cover sent to Discord");
Check(fake.LastUpload.SequenceEqual(imageBytes), "announcement multipart contains original image bytes");
Check(fake.LastMessage.GetProperty("embeds")[0].GetProperty("image").GetProperty("url").GetString() == "attachment://event-banner.png", "embed references attached banner");
Check(fake.LastMessage.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength() == 0, "announcement suppresses unsolicited mentions");
Check(fake.LastMessage.GetProperty("components")[0].GetProperty("components")[0].GetProperty("style").GetInt32() == 5, "announcement includes working event link button");
await relay.Publish(login, illustrated.Id, illustrated);
Check(!fake.LastEvent.TryGetProperty("image", out _), "no new banner preserves existing event cover");
Check(fake.LastMessage.GetProperty("attachments")[0].GetProperty("id").GetString() == "999", "edit preserves existing announcement attachment");
await relay.Publish(login, illustrated.Id, illustrated, "");
Check(fake.LastEvent.GetProperty("image").ValueKind == JsonValueKind.Null && fake.LastMessage.GetProperty("attachments").GetArrayLength() == 0, "remove banner clears event and announcement images");
File.WriteAllText(Path.Combine(root, "relay-defaults.json"), "{\"RelayUrl\":\"https://example.com\"}");
Check(RelayDefaults.Read(root) == "https://example.com/", "distributed relay address normalizes automatically");
File.WriteAllText(Path.Combine(root, "relay-defaults.json"), "{\"RelayUrl\":\"\"}");
Check(RelayDefaults.Read(root) == "", "undeployed relay is not fabricated");
File.WriteAllText(Path.Combine(root, "relay-defaults.json"), "{\"RelayUrl\":\"http://example.com\"}");
await Reject(() => { RelayDefaults.Read(root); return Task.CompletedTask; }, "shared relay requires HTTPS");
var pickerType = typeof(BannerPicker).GetNestedType("OpenFileName", System.Reflection.BindingFlags.NonPublic)!;
Check(System.Runtime.InteropServices.Marshal.SizeOf(pickerType) == (IntPtr.Size == 8 ? 152 : 88), "Windows picker native structure has correct size");
var native = System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf(pickerType));
try { System.Runtime.InteropServices.Marshal.StructureToPtr(Activator.CreateInstance(pickerType)!, native, false); Check(true, "Windows picker structure marshals successfully"); }
finally { System.Runtime.InteropServices.Marshal.DestroyStructure(native, pickerType); System.Runtime.InteropServices.Marshal.FreeHGlobal(native); }
var lockStore = new EventStore(Path.Combine(root, "locking"));
Check(lockStore.StayConnectedWhilePlaying, "play-session renewal enabled by default");
lockStore.StayConnectedWhilePlaying = false;
lockStore.Save();
Check(!new EventStore(Path.Combine(root, "locking")).StayConnectedWhilePlaying, "play-session opt-out survives reload");
Check(lockStore.RelayAddressLocked, "relay address locked by default");
lockStore.RelayAddressLocked = false; lockStore.Save();
Check(!new EventStore(Path.Combine(root, "locking")).RelayAddressLocked, "custom relay unlock persists");
var card = Make(); card.Title = "<script>alert(1)</script>";
var html = EventCardTemplate.Html(card, "<img src=x>", imageData);
Check(!html.Contains("<script>") && html.Contains("&lt;script&gt;"), "card encodes user text");
Check(html.Contains("default-src 'none'") && html.Contains("img-src data:"), "card blocks external image requests");
await Reject(() => { EventCardTemplate.Html(card, "Tester", "https://example.com/image.png"); return Task.CompletedTask; }, "card refuses arbitrary image URLs");
var composedConfig = new ConfigurationBuilder().AddConfiguration(config).AddInMemoryCollection(new Dictionary<string, string?> { ["EVENT_DATA_PATH"] = Path.Combine(root, "composed.json") }).Build();
var cardFake = new FakeRenderer(imageBytes); var cardDiscord = new FakeDiscord();
var cardRelay = new Relay(composedConfig, cardDiscord, cardFake);
card = Make();
await cardRelay.Publish(login, card.Id, card, imageData);
Check(cardDiscord.LastUpload.SequenceEqual(imageBytes) && cardDiscord.LastMessage.GetProperty("embeds")[0].GetProperty("image").GetProperty("url").GetString() == "attachment://event-card.png", "composition uploads one card attachment");
Check(!cardDiscord.LastMessage.GetProperty("embeds")[0].TryGetProperty("description", out _), "composed card avoids duplicate embed text");
Check(cardDiscord.LastEvent.GetProperty("image").GetString() == imageData, "native event keeps original banner");
cardRelay = new Relay(composedConfig, cardDiscord, cardFake);
await cardRelay.Publish(login, card.Id, card);
Check(cardFake.LastBanner == imageData && cardDiscord.Creates == 1 && cardDiscord.MessageEdits == 1, "restart preserves source banner and edits same post");
await cardRelay.Publish(login, card.Id, card, "");
Check(cardFake.LastBanner == "" && cardDiscord.LastEvent.GetProperty("image").ValueKind == JsonValueKind.Null, "remove source banner rerenders card");
cardFake.Fail = true;
var mutationCount = cardDiscord.Creates + cardDiscord.Edits + cardDiscord.Posts + cardDiscord.MessageEdits;
var failedCard = Make();
await Reject(async () => { await cardRelay.Publish(login, failedCard.Id, failedCard); }, "renderer failure reported before publication");
Check(mutationCount == cardDiscord.Creates + cardDiscord.Edits + cardDiscord.Posts + cardDiscord.MessageEdits, "renderer failure sends no Discord writes");
// A local preview of the same production template, with clearly illustrative content.
card = Make(); card.Title = "Moonlit gathering"; card.Organizer = "Kaerl"; card.Server = "Event Horizon community";
card.Description = "Meet beneath the stars for an evening in the Lavender Beds. Bring your friends, share a story, and enjoy the company.\n\nGather ten minutes early by the central garden.";
card.SignupGroups = "Tanks: 2 · Healers: 4 · Damage: 14";
File.WriteAllText(Path.Combine(root, "event-card-preview.html"), EventCardTemplate.Html(card, card.Organizer, null,
    Convert.ToBase64String(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "card-assets", "Cinzel.ttf"))),
    Convert.ToBase64String(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "card-assets", "icon.png")))));
Console.WriteLine($"{passed} checks passed. Test artifacts: {root}");

sealed class FakeRenderer(byte[] bytes) : IEventCardRenderer
{
    public bool Fail;
    public string? LastBanner;
    public Task<byte[]> Render(EventRecord item, string organizer, string? banner)
    { if (Fail) throw new HttpRequestException("simulated rendering failure"); LastBanner = banner; return Task.FromResult(bytes); }
}

sealed class FakeDiscord : HttpMessageHandler
{
    public bool DenyChannel, DenyGuild, FailCreate, FailMessage;
    public int Creates, Posts, Edits, MessageEdits;
    public JsonElement LastEvent, LastMessage;
    public byte[] LastUpload = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var path = request.RequestUri!.AbsolutePath;
        var method = request.Method;
        if (request.Content is MultipartFormDataContent multipart)
        {
            foreach (var part in multipart)
            {
                if (part.Headers.ContentDisposition?.Name?.Trim('"') == "payload_json") LastMessage = JsonSerializer.Deserialize<JsonElement>(await part.ReadAsStringAsync(token));
                else LastUpload = await part.ReadAsByteArrayAsync(token);
            }
        }
        else if (request.Content is not null && request.Content.Headers.ContentType?.MediaType == "application/json")
        {
            var payload = JsonSerializer.Deserialize<JsonElement>(await request.Content.ReadAsStringAsync(token));
            if (path.Contains("scheduled-events")) LastEvent = payload;
            else if (path.Contains("messages")) LastMessage = payload;
        }
        object data;
        if (path.EndsWith("oauth2/token")) data = new { access_token = "test-user", expires_in = 3600 };
        else if (path.EndsWith("users/@me/guilds")) data = new[] { new { id = "123", name = "Community", owner = false, permissions = DenyGuild ? "0" : "17592186062848" } };
        else if (path.EndsWith("users/@me")) data = new { id = request.Headers.Authorization?.Scheme == "Bot" ? "99" : "42", username = "Tester" };
        else if (path.EndsWith("/roles")) data = new[] { new { id = "123", permissions = "17592186096640" } };
        else if (path.Contains("/members/")) data = new { roles = Array.Empty<string>() };
        else if (path.EndsWith("guilds/123/channels")) data = new[] { new { id = "456", name = "events", type = 0, permission_overwrites = DenyChannel ? new[] { new { id = "42", type = 1, allow = "0", deny = "1024" } } : Array.Empty<object>() } };
        else if (path.EndsWith("guilds/123")) data = new { owner_id = "7" };
        else if (path.EndsWith("scheduled-events") && method == HttpMethod.Post)
        { Creates++; if (FailCreate) throw new HttpRequestException("simulated lost response"); data = new { id = "777" }; }
        else if (path.EndsWith("scheduled-events/777") && method == HttpMethod.Patch) { Edits++; data = new { id = "777" }; }
        else if (path.EndsWith("/messages") && method == HttpMethod.Post) { Posts++; if (FailMessage) throw new HttpRequestException("simulated lost announcement response"); data = new { id = "888", attachments = new[] { new { id = "999", filename = "event-banner.png" } } }; }
        else if (path.EndsWith("/messages/888") && method == HttpMethod.Patch) { MessageEdits++; data = new { id = "888", attachments = new[] { new { id = "999", filename = "event-banner.png" } } }; }
        else throw new Exception("Unexpected test route: " + method + " " + path);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(data), System.Text.Encoding.UTF8, "application/json") };
    }
}
