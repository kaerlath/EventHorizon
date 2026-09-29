using System.Diagnostics;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace EventHorizon;

public sealed partial class MainWindow : Window, IDisposable
{
    private static readonly Vector4 Muted = new(.58f, .67f, .8f, 1f);
    private readonly EventStore store;
    private readonly ITextureProvider textures;
    private readonly string iconPath;
    private readonly string defaultRelayAddress = "";
    private readonly RelayClient relay = new();
    private EventRecord editor = new();
    private Guid? selected;
    private string section = "Events", message = "", search = "", relayAddress;
    private bool editing, preview;
    private Action? afterDiscard;
    private LinkStart? link;
    private DiscordChoice[] guilds = [], channels = [];
    // All UI/store mutations are applied in Draw; workers return a main-thread action.
    private Task<Action>? operation;
    private bool Busy => operation is not null;

    public MainWindow(EventStore store, ITextureProvider textures, string assemblyDirectory, IUiBuilder ui) : base("Event Horizon###EventHorizonMain")
    {
        this.store = store; this.textures = textures;
        if (store.ShowWelcomeHelp) section = "Help";
        iconPath = Path.Combine(assemblyDirectory, "icon.png");
        InitializeFonts(ui, assemblyDirectory);
        try
        {
            defaultRelayAddress = RelayDefaults.Read(assemblyDirectory);
            if (store.RelayAddressLocked && defaultRelayAddress.Length > 0) { store.RelayUrl = defaultRelayAddress; store.Save(); }
        }
        catch (Exception ex) { message = "Could not load the default connection: " + ex.Message; }
        relayAddress = store.RelayUrl;
        if (relayAddress.Length > 0) try { relay.Configure(relayAddress); } catch (ArgumentException) { }
        Size = new Vector2(1120, 760); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(850, 580), MaximumSize = new Vector2(float.MaxValue) };
    }
    public void OpenSettings() { RestoreWindow(); Navigate(() => { section = "Settings"; selected = null; editing = false; }); }
    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, BackgroundSurface(.015f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Border, Surface(.42f));
        ImGui.PushStyleColor(ImGuiCol.Button, Surface(.28f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Surface(.45f));
        ImGui.PushStyleColor(ImGuiCol.Header, Surface(.32f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Surface(.10f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Surface(.55f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Surface(.40f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, Surface(.5f));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Accent);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Accent);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Vector4.Lerp(Accent, Vector4.One, .4f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10, 9));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 6));
    }
    public override void PostDraw() { ImGui.PopStyleVar(4); ImGui.PopStyleColor(13); FinishRestoreGeometry(); }
    private void ProcessPendingOperation()
    {
        if (operation is { IsCompleted: true })
        {
            var completed = operation; operation = null;
            try { completed.GetAwaiter().GetResult()(); }
            catch (Exception ex) { message = ex.Message; }
        }
    }
    public override void Draw()
    {
        using var body = bodyFont?.Push();
        ProcessPendingOperation();
        ResetCommunitySession();
        DrawHeader();
        ImGui.BeginDisabled(Busy || ConfirmationPending);
        ImGui.BeginChild("Navigation", new Vector2(220, -42), true);
        PanelBackdrop();
        ImGui.Spacing();
        foreach (var name in new[] { "Events", "Drafts", "Recurring", "Templates", "History" })
        {
            var count = Visible(name).Length;
            if (NavigationItem(name, count))
                Navigate(() => { section = name; editing = false; selected = null; });
        }
        ImGui.Spacing(); ImGui.Separator();
        using (navIconFont?.Push()) ImGui.TextColored(Accent, char.ConvertFromUtf32((int)FontAwesomeIcon.Comments));
        ImGui.SameLine(); ImGui.TextUnformatted("Discord");
        ConnectionBadge(true);
        ImGui.TextWrapped(relay.Connected ? relay.UserName : "Not connected");
        ImGui.TextWrapped(store.GuildName.Length > 0 ? store.GuildName : "Choose your community");
        if (ImGui.Button(relay.Connected ? "Connection settings" : "Connect Discord", new Vector2(-1, 32))) OpenSettings();
        if (ImGui.Selectable("Appearance", section == "Appearance")) Navigate(() => { section = "Appearance"; editing = false; selected = null; });
        if (ImGui.Selectable("Reminders", section == "Reminders")) Navigate(() => { section = "Reminders"; editing = false; selected = null; });
        if (ImGui.Selectable("Help & Walkthrough", section == "Help")) OpenHelp();
        if (ImGui.Selectable("About", section == "About")) Navigate(() => { section = "About"; editing = false; selected = null; });
        ImGui.Spacing();
        if (ImGui.Button("+ Create Event", new Vector2(-1, 38))) Navigate(() => BeginEdit(NewRecord()));
        ImGui.EndChild(); ImGui.SameLine();
        ImGui.BeginChild("Workspace", new Vector2(0, -42), true);
        PanelBackdrop();
        if (store.LoadError is not null) ImGui.TextWrapped("Saved data cannot be read. Editing is disabled to protect it: " + store.LoadError);
        else if (editing) DrawEditor();
        else if (section == "Settings") DrawSettings();
        else if (section == "Appearance") DrawAppearance();
        else if (section == "Reminders") DrawReminderSettings();
        else if (section == "Help") DrawHelp();
        else if (section == "About")
        {
            DrawOrb(110); ImGui.TextColored(Accent, "EVENT HORIZON  /  0.16.1");
            ImGui.TextUnformatted("Title font: " + titleFontName);
            ImGui.Checkbox("Animate gravity drive", ref animateOrb);
            ImGui.TextWrapped("A space-gothic event workspace for Eorzea. Plan locally, then publish native Discord events and rich announcements through your own relay.");
            ImGui.TextWrapped("Plan community or personal events, choose in-game reminders or optional Discord DMs, sync Google calendars, and delete your published events. Live role signups and automatic recurring publication are future work.");
        }
        else if (selected is Guid id && Find(id) is { } item)
        {
            var available = ImGui.GetContentRegionAvail().X;
            var width = Math.Min(1420, available);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (available - width) / 2));
            ImGui.BeginChild("EventPresentation", new Vector2(width, 0), false);
            DrawDetails(item);
            ImGui.EndChild();
        }
        else DrawList();
        ImGui.EndChild(); ImGui.EndDisabled();
        ImGui.TextWrapped(Busy ? "Working with the relay…" : message.Length > 0 ? message : "Drafts stay on this computer. Discord changes are sent only when you publish or sync.");
        DrawRenderedPreview();
        DrawHelpOverlay();
    }
    private void Navigate(Action action)
    {
        if (ConfirmationPending) return;
        if (editing) { afterDiscard = action; }
        else { message = ""; action(); }
    }
    private EventRecord NewRecord() => new()
    {
        StartLocal = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd HH:mm"), TimeZoneId = TimeZoneInfo.Local.Id,
        GuildId = store.GuildId, Server = store.GuildName, ChannelId = store.ChannelId, Channel = store.ChannelName,
        Organizer = relay.UserName
    };
    private void BeginEdit(EventRecord item) { if (IsReadOnly(item)) { message = "Only the publishing account can edit this event."; return; } textStyleTarget = 0; editor = item.Copy(); editing = true; preview = false; message = ""; }
    private EventRecord? Find(Guid id) => EventLibrary().Concat(store.Templates).Concat(OfficialEvents.Items).FirstOrDefault(x => x.Id == id);
    private EventRecord[] Visible(string name) => (name == "Templates" ? store.Templates : EventLibrary().Where(e => name switch
    {
        "Drafts" => e.Status == "Draft",
        "Recurring" => e.Recurrence != "None" && e.Status is not ("Archived" or "Deleting" or "Deleted"),
        "History" => e.Status is "Archived" or "Deleting" or "Deleted" || (e.Status != "Draft" && EventRules.HasEnded(e)),
        _ => e.Status is not ("Archived" or "Deleting" or "Deleted") && e.Status != "Draft" && !EventRules.HasEnded(e)
    })).Where(e => string.IsNullOrWhiteSpace(search) || (e.Title + " " + e.World + " " + e.Location).Contains(search, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(e => e.UpdatedUtc).ToArray();
    private void DrawList()
    {
        ImGui.TextColored(Accent, section.ToUpperInvariant());
        ImGui.TextColored(Muted, section == "Events" ? "Your next gathering starts here." : "Your community, thoughtfully organized.");
        if (section == "Events") DrawCommunityToolbar();
        ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##search", "Search title, world or location", ref search, 200);
        ImGui.Separator();
        var list = section == "Events" ? DrawEventCalendar() : Visible(section);
        if (section == "Events" && calendarDetailId is Guid detailId)
        {
            var detail = list.FirstOrDefault(e => e.Id == detailId);
            if (detail is null) calendarDetailId = null;
            else
            {
                ImGui.BeginChild("SelectedCalendarEvent", new Vector2(-1, 650), true);
                DrawDetails(detail, true);
                ImGui.EndChild(); ImGui.Spacing();
            }
        }
        if (list.Length == 0 && section != "Events")
        {
            ImGui.Spacing(); DrawOrb(165); Heading("A new gathering awaits");
            ImGui.TextWrapped(section == "Events" ? "Create an event or finish a draft. Published events appear here; Settings connects your Discord community." : "No matching entries here yet.");
            ImGui.Spacing();
            if (ImGui.Button("Create your first event", new Vector2(240, 42))) BeginEdit(NewRecord());
            ImGui.SameLine(); if (ImGui.Button("Browse drafts", new Vector2(170, 42))) section = "Drafts";
        }
        foreach (var item in list)
        {
            ImGui.PushID(item.Id.ToString()); ImGui.BeginChild("Card", new Vector2(-1, 112), true);
            ImGui.TextColored(Accent, item.Title);
            var localTime = EventCalendar.Interval(item, TimeZoneInfo.Local);
            ImGui.TextColored(Muted, localTime is { } t ? $"{t.Start:ddd, MMM d · h:mm tt} – {t.End:MMM d · h:mm tt}" : item.StartLocal);
            ImGui.TextUnformatted($"{item.World}  {item.Location}  /  {item.Status}");
            if (ImGui.SmallButton("View")) { if (section == "Events") calendarDetailId = item.Id; else selected = item.Id; }
            ImGui.SameLine(); ImGui.BeginDisabled(IsReadOnly(item)); if (ImGui.SmallButton(section == "Templates" ? "Use template" : "Edit"))
            { if (section == "Templates") CreateCopy(item); else BeginEdit(item); }
            ImGui.EndDisabled(); ImGui.EndChild(); ImGui.PopID();
        }
    }
    private void CreateCopy(EventRecord item)
    {
        var copy = item.Copy(); copy.Id = Guid.NewGuid(); copy.Status = "Draft"; copy.ReadOnly = false; copy.GoogleCalendarSync = false; copy.DiscordRemindersEnabled = false;
        copy.DiscordEventId = ""; copy.DiscordMessageId = ""; copy.RelayOrigin = ""; BeginEdit(copy);
    }
    private void DrawDetails(EventRecord item, bool inline = false)
    {
        ImGui.TextColored(Muted, section + "  >  View event");
        if (ActionButton("Back", FontAwesomeIcon.ArrowLeft, 100)) { if (inline) calendarDetailId = null; else selected = null; return; }
        if (item.InformationOnly) { DrawOfficialDetails(item); return; }
        ImGui.SameLine(); ImGui.BeginDisabled(IsReadOnly(item)); if (ActionButton("Edit event", FontAwesomeIcon.Edit, 155, true)) BeginEdit(item); ImGui.EndDisabled();
        ImGui.SameLine(); if (ActionButton("Duplicate", FontAwesomeIcon.Copy, 155)) CreateCopy(item);
        ImGui.Spacing(); Heading(item.Title);
        if (item.Status is "Deleting" or "Deleted") { ImGui.TextWrapped("This event has been withdrawn. Check status below for remaining Discord or Google cleanup."); DrawDeletionControls(item); return; }
        DrawSchedule(item);
        ImGui.TextColored(EventTypes.For(item).Color, EventTypes.For(item).Name);
        if (!string.IsNullOrWhiteSpace(item.Tags)) ImGui.TextWrapped("Tags: " + item.Tags);
        if (item.DiscordEventId.Length > 0)
        {
            ImGui.Checkbox("Show announcement artwork", ref showAnnouncement);
        }
        ImGui.Spacing();
        var available = ImGui.GetContentRegionAvail().X;
        var sideWidth = available > 850 ? 290f : 230f;
        ImGui.BeginChild("EventBody", new Vector2(Math.Max(300, available - sideWidth - 14), 0), false);
        var artworkVisible = showAnnouncement && item.DiscordEventId.Length > 0 && DrawEventArtwork(item);
        if (!artworkVisible)
        {
        DrawBanner(item, Math.Clamp(store.Appearance.BannerHeight, 240, 600));
        ImGui.BeginChild("DescriptionCard", new Vector2(0, Math.Max(110, ImGui.CalcTextSize(item.Description, false, ImGui.GetContentRegionAvail().X - 30).Y + 34)), true);
        PanelBackdrop();
        ImGui.TextWrapped(item.Description); ImGui.EndChild();
        if (!item.PersonalOnly) {
        ImGui.Spacing(); Heading("Planned signup groups");
        ImGui.TextColored(Muted, "Mark your interest on Discord. Live role attendance is not available yet.");
        var groups = item.SignupGroups.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (ImGui.BeginTable("AttendanceCards", ImGui.GetContentRegionAvail().X > 760 ? 3 : 2, ImGuiTableFlags.SizingStretchSame))
        {
            for (var i = 0; i < groups.Length; i++)
            {
                ImGui.TableNextColumn(); ImGui.PushID(i);
                ImGui.BeginChild("Group", new Vector2(0, 112), true);
                PanelBackdrop();
                var parts = groups[i].Split(':', 2);
                ImGui.TextColored(Accent, parts[0]);
                ImGui.TextUnformatted(parts.Length > 1 ? "Capacity: " + parts[1] : "Open group");
                ImGui.TextDisabled("No live attendance data");
                ImGui.EndChild(); ImGui.PopID();
            }
            ImGui.EndTable();
        }
        }
        ImGui.Spacing();
        ImGui.BeginDisabled(IsReadOnly(item));
        if (ImGui.Button("Edit / Update event", new Vector2(190, 36))) BeginEdit(item);
        ImGui.EndDisabled();
        ImGui.SameLine(); if (ImGui.Button("Create copy", new Vector2(140, 36))) CreateCopy(item);
        }
        ImGui.EndChild(); ImGui.SameLine();
        ImGui.BeginChild("EventDetailsCard", new Vector2(0, 0), true);
        PanelBackdrop();
        ImGui.TextColored(Accent, "EVENT DETAILS"); ImGui.Separator();
        Detail("Visibility", item.PersonalOnly ? "Personal — only me" : "Community");
        Detail("Organizer", item.Organizer);
        if (!item.PersonalOnly) { Detail("Server", item.Server); Detail("Channel", item.Channel); }
        Detail("Location", item.World + " — " + item.Location); Detail("Signups close", item.SignupsClose); Detail("Status", item.Status);
        if (item.Status != "Archived" && item.Status != "Template") DrawEventReminder(item);
        if (item.PersonalOnly) { DrawPrivateCalendarActions(item); DrawDiscordReminders(item); }
        if (item.DiscordEventId.Length > 0)
        {
            if (ImGui.CollapsingHeader("My Google Calendar")) DrawPersonalCalendarActions(item);
            if (ImGui.Button("Open in Discord", new Vector2(-1, 36))) OpenUrl($"https://discord.com/events/{item.GuildId}/{item.DiscordEventId}");
            if (ImGui.Button("Copy event link", new Vector2(-1, 36))) { ImGui.SetClipboardText($"https://discord.com/events/{item.GuildId}/{item.DiscordEventId}"); message = "Event link copied."; }
        }
        DrawDeletionControls(item);
        if (item.PersonalOnly && item.DiscordRemindersEnabled) ImGui.TextWrapped("Turn off Discord DM reminders below their heading before archiving.");
        if (!IsReadOnly(item) && item.Status != "Archived" && (!item.PersonalOnly || !item.DiscordRemindersEnabled) && ImGui.Button("Archive locally", new Vector2(-1, 36)))
        {
            try { var archived = item.Copy(); archived.Status = "Archived"; store.Upsert(archived, section == "Templates"); message = item.PersonalOnly ? "Archived locally. Any Google copy is unchanged." : "Archived locally. The Discord event is unchanged."; selected = null; }
            catch (Exception ex) { message = ex.Message; }
        }
        ImGui.EndChild();
    }
    private static void Detail(string label, string value)
    {
        ImGui.TextColored(Muted, label); ImGui.TextWrapped(string.IsNullOrWhiteSpace(value) ? "—" : value); ImGui.Spacing();
    }
    private void DrawSchedule(EventRecord item)
    {
        if (item.ScheduleMode != "Single") { DrawSessionSchedule(item); return; }
        if (DateTime.TryParseExact(item.StartLocal, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
        {
            using (navIconFont?.Push()) ImGui.TextColored(Accent, char.ConvertFromUtf32((int)FontAwesomeIcon.CalendarAlt));
            ImGui.SameLine(); ImGui.TextUnformatted(date.ToString("dddd, MMMM d, yyyy"));
            var end = date.AddMinutes(item.DurationMinutes);
            var endLabel = end.Date == date.Date ? end.ToString("h:mm tt") : end.ToString("ddd, MMM d · h:mm tt");
            using (navIconFont?.Push()) ImGui.TextColored(Accent, char.ConvertFromUtf32((int)FontAwesomeIcon.Clock));
            ImGui.SameLine(); ImGui.TextWrapped($"{date:h:mm tt} – {endLabel}  ·  {item.DurationMinutes / 60f:0.##} hours  ·  {item.TimeZoneId}");
        }
        else ImGui.TextUnformatted(item.StartLocal);
    }
    private void DrawBanner(EventRecord item, float height)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var banner = File.Exists(item.BannerPath) ? textures.GetFromFile(item.BannerPath).GetWrapOrDefault() : null;
        var top = ImGui.GetCursorScreenPos();
        if (banner is not null)
        {
            var scale = Math.Min(width / banner.Width, height / banner.Height);
            var display = new Vector2(banner.Width, banner.Height) * scale;
            var offset = new Vector2((width - display.X) / 2, (height - display.Y) / 2);
            var draw = ImGui.GetWindowDrawList();
            draw.AddRectFilled(top, top + new Vector2(width, height), 0xFF160E08, 10);
            draw.AddImage(banner.Handle, top + offset, top + offset + display);
            ImGui.Dummy(new Vector2(width, height));
        }
        else
        {
            var draw = ImGui.GetWindowDrawList();
            draw.AddRectFilledMultiColor(top, top + new Vector2(width, height), 0xFF382514, 0xFF1E1009, 0xFF2F1822, 0xFF100807);
            for (var i = 0; i < 65; i++) draw.AddCircleFilled(top + new Vector2((i * 137.3f) % width, (i * 73.1f) % height), i % 9 == 0 ? 1.5f : .7f, 0x778CA8DE, 6);
            ImGui.SetCursorScreenPos(top + new Vector2(width / 2 - 65, height / 2 - 65)); DrawOrb(130);
            ImGui.SetCursorScreenPos(top); ImGui.Dummy(new Vector2(width, height));
        }
        ImGui.GetWindowDrawList().AddRect(top, top + new Vector2(width, height), 0xFF71523F, 10);
        if (!string.IsNullOrWhiteSpace(item.World + item.Location))
            ImGui.TextWrapped(item.World + "  /  " + item.Location);
    }
    private void DrawPreview(EventRecord item)
    {
        Heading(string.IsNullOrWhiteSpace(item.Title) ? "Untitled gathering" : item.Title);
        DrawSchedule(item); ImGui.Spacing(); DrawBanner(item, Math.Clamp(store.Appearance.BannerHeight, 240, 600)); ImGui.Spacing();
        ImGui.TextWrapped(item.Description);
        if (item.PersonalOnly) { ImGui.TextWrapped("Personal event — no Discord post. Banner stays on this computer."); return; }
        ImGui.Separator(); ImGui.TextColored(Muted, "DISCORD POST PREVIEW");
        ImGui.TextWrapped($"Organizer: {item.Organizer}\nLocation: {item.World} — {item.Location}");
        ImGui.TextWrapped("Your relay combines the banner and event details into the Discord announcement. A composed card includes the title, description, date/time, organizer and location, with a working event link beneath it.");
    }
    private void DrawEditor()
    {
        ImGui.TextColored(Muted, "Events  >  Edit event");
        Heading(string.IsNullOrWhiteSpace(editor.Title) ? "Create your gathering" : editor.Title);
        DrawVisibilityChoice();
        ImGui.Checkbox("Preview", ref preview);
        ImGui.BeginChild("EditorScroll", new Vector2(0, -82), false);
        if (preview) DrawPreview(editor);
        else
        {
            Text("Title", editor.Title, 101, x => editor.Title = x);
            TextArea("Description", editor.Description, 1001, 100, x => editor.Description = x);
            if (!editor.PersonalOnly) {
            DrawAnnouncementStyles();
            ImGui.BeginDisabled(!relay.Connected);
            if (ImGui.Button("Render announcement preview", new Vector2(270, 36))) RequestStyledPreview();
            ImGui.EndDisabled();
            }
            ImGui.Separator(); ImGui.TextColored(Accent, "WHEN & WHERE");
            DrawEventCategoryEditor();
            DrawScheduleEditor();
            if (ImGui.BeginCombo("Time zone", editor.TimeZoneId))
            {
                foreach (var zone in TimeZoneInfo.GetSystemTimeZones()) if (ImGui.Selectable(zone.DisplayName, editor.TimeZoneId == zone.Id)) editor.TimeZoneId = zone.Id;
                ImGui.EndCombo();
            }
            if (editor.ScheduleMode == "Single" && ImGui.BeginCombo("Duration", $"{editor.DurationMinutes / 60}h {editor.DurationMinutes % 60:00}m"))
            { foreach (var minutes in new[] { 15, 30, 45, 60, 90, 120, 150, 180, 240, 300, 360, 480, 720, 1440 }) if (ImGui.Selectable($"{minutes / 60}h {minutes % 60:00}m", editor.DurationMinutes == minutes)) editor.DurationMinutes = minutes; ImGui.EndCombo(); }
            Text("World", editor.World, 80, x => editor.World = x); Text("Location", editor.Location, 101, x => editor.Location = x);
            Text("Organizer", editor.Organizer, 100, x => editor.Organizer = x);
            ImGui.TextWrapped(editor.PersonalOnly ? "Personal event — visible only in this installation and your optional Google copy." : "Use an in-game or display name. Editing access stays with the Discord account that published the event.");
            DrawBannerSelector();
            if (!editor.PersonalOnly) {
            ImGui.TextWrapped("Personal calendar: after publishing, open the event and choose Add to my Google Calendar. Other players can subscribe without editing your event.");
            ImGui.Separator(); ImGui.TextColored(Accent, "DISCORD DESTINATION");
            ImGui.TextWrapped($"{editor.Server}  /  {editor.Channel}");
            if (editor.DiscordEventId.Length == 0 && ImGui.Button("Use destination from Settings"))
            { editor.GuildId = store.GuildId; editor.Server = store.GuildName; editor.ChannelId = store.ChannelId; editor.Channel = store.ChannelName; }
            ImGui.TextDisabled("Choose the server and optional announcement channel in Settings before publishing.");
            if (ImGui.CollapsingHeader("Signup and recurrence planning"))
            {
                if (ImGui.Button("Full party")) editor.SignupGroups = "Tank:2\nHealer:2\nDamage:4";
                ImGui.SameLine(); if (ImGui.Button("Open gathering")) editor.SignupGroups = "Guests:unlimited";
                TextArea("Groups (Name:capacity)", editor.SignupGroups, 600, 70, x => editor.SignupGroups = x);
                Text("Signups close (planning)", editor.SignupsClose, 100, x => editor.SignupsClose = x);
                if (ImGui.BeginCombo("Recurrence (planning)", editor.Recurrence))
                { foreach (var option in new[] { "None", "Weekly", "Monthly" }) if (ImGui.Selectable(option, editor.Recurrence == option)) editor.Recurrence = option; ImGui.EndCombo(); }
                ImGui.TextWrapped("Custom signups and recurring publication require a later relay extension. These fields are saved locally.");
            }
        }
        }
        ImGui.EndChild();
        if (ImGui.Button(editor.PersonalOnly ? "Save personal event" : editor.DiscordEventId.Length > 0 ? "Save local changes" : "Save draft")) Save(false);
        ImGui.SameLine(); if (ImGui.Button("Save as template")) Save(true);
        ImGui.SameLine(); if (ImGui.Button("Cancel")) Navigate(() => editing = false);
        if (editor.PersonalOnly) { DrawPersonalSaveButton(); return; }
        ImGui.BeginDisabled(!relay.Connected);
        if (ImGui.Button(editor.DiscordEventId.Length > 0 ? "Save & Sync to Discord" : "Publish to Discord", new Vector2(230, 30))) Publish();
        ImGui.EndDisabled();
    }
    private static void Text(string label, string value, int limit, Action<string> set)
    { ImGui.TextColored(Muted, label); ImGui.SetNextItemWidth(-1); if (ImGui.InputText("##" + label, ref value, limit)) set(value); }
    private static void TextArea(string label, string value, int limit, float height, Action<string> set)
    { ImGui.TextColored(Muted, label); if (ImGui.InputTextMultiline("##" + label, ref value, limit, new Vector2(-1, height))) set(value); }
    private void DrawSettings()
    {
        DrawGoogleSettings();
        ImGui.Separator();
        Heading("Connect your community");
        DrawBotInstallation();
        ImGui.TextWrapped("Account authorization opens in your browser. Discord passwords and bot credentials are never entered in this plugin.");
        ImGui.Separator();
        if (relay.Origin.Length == 0)
        {
            ImGui.TextWrapped("The shared Event Horizon service has not been deployed yet. Once it is available, the plugin will fill in its address automatically.");
        }
        else ImGui.TextWrapped("Service: " + relay.Origin);
        var locked = store.RelayAddressLocked;
        if (ImGui.Checkbox("Lock relay address", ref locked))
        {
            store.RelayAddressLocked = locked;
            if (locked && defaultRelayAddress.Length > 0)
            {
                relayAddress = defaultRelayAddress; relay.Configure(relayAddress); store.RelayUrl = relay.Origin;
                link = null; guilds = []; channels = [];
            }
            TrySaveSettings();
        }
        ImGui.TextDisabled("Uncheck to use a custom relay. Locking restores the shared service when available.");
        ImGui.BeginDisabled(locked);
        Text("Relay address", relayAddress, 400, x => relayAddress = x);
        if (ImGui.Button("Save relay address"))
        {
            try { relay.Configure(relayAddress); store.RelayUrl = relay.Origin; store.Save(); link = null; guilds = []; channels = []; message = "Relay saved. Connect your account next."; }
            catch (Exception ex) { message = ex.Message; }
        }
        ImGui.SameLine();
        if (ImGui.Button("Use local test relay"))
        { relayAddress = "http://localhost:5187"; relay.Configure(relayAddress); store.RelayUrl = relay.Origin; TrySaveSettings(); link = null; guilds = []; channels = []; }
        ImGui.EndDisabled();
        ImGui.Separator();
        if (!relay.Connected)
        {
            ImGui.TextColored(Accent, "1. LINK YOUR DISCORD ACCOUNT");
            ImGui.BeginDisabled(relay.Origin.Length == 0 || !gameLoggedIn);
            if (ImGui.Button("Connect Discord account", new Vector2(240, 38))) Run(async () =>
            {
                var generation = connectionGeneration;
                var result = await relay.Send<LinkStart>(HttpMethod.Post, "auth/link", authenticated: false);
                if (!Uri.TryCreate(result.VerificationUrl, UriKind.Absolute, out var uri) || uri.GetLeftPart(UriPartial.Authority) + "/" != relay.Origin)
                    throw new InvalidOperationException("The relay returned an unexpected authorization address.");
                return () => { if (generation != connectionGeneration) return; link = result; message = "Click Open authorization page, approve in Discord, then Finish connection."; };
            });
            ImGui.EndDisabled();
            if (link is { } pending)
            {
                ImGui.TextUnformatted("Connection reference: " + pending.Code);
                if (ImGui.Button("Open authorization page")) OpenUrl(pending.VerificationUrl);
                ImGui.SameLine(); if (ImGui.Button("Finish connection")) Run(async () =>
                {
                    var generation = connectionGeneration;
                    var origin = relay.Origin;
                    var result = await relay.Send<LinkResult>(HttpMethod.Post, "auth/poll", new { pending.PollToken }, false);
                    return () =>
                    {
                        if (generation != connectionGeneration || !gameLoggedIn) { if (result.Token is { Length: > 0 }) _ = RevokeSession(origin, result.Token); return; }
                        if (result.Status == "connected" && !string.IsNullOrEmpty(result.Token))
                        { relay.Token = result.Token; relay.UserName = result.UserName ?? "Discord account"; link = null; message = "Connected. Load your servers below."; }
                        else message = "Waiting for Discord authorization. Complete it in your browser first.";
                    };
                });
            }
        }
        else
        {
            ImGui.TextColored(Accent, "CONNECTED AS " + relay.UserName);
            if (ImGui.Button("Disconnect")) Run(async () =>
            {
                try { await relay.Send<object>(HttpMethod.Post, "auth/logout"); } catch { }
                return () => { relay.Token = ""; relay.UserName = ""; guilds = []; channels = []; message = "Disconnected on this device."; };
            });
            ImGui.SameLine(); if (ImGui.Button("Load servers")) Run(async () =>
            { var result = await relay.Send<DiscordChoice[]>(HttpMethod.Get, "discord/guilds"); return () => { guilds = result; message = result.Length == 0 ? "No eligible servers. Install the relay bot and grant Create Events permission." : "Choose a server."; }; });
            if (ImGui.Button("Recover missing published events")) Run(async () =>
            {
                var result = await relay.Send<EventRecord[]>(HttpMethod.Get, "events");
                return () =>
                {
                    var count = 0;
                    foreach (var item in result.Where(x => store.Events.All(e => e.Id != x.Id))) { store.Upsert(item); count++; }
                    message = $"Recovered {count} published events. Existing local edits were preserved.";
                };
            });
            ImGui.Separator(); ImGui.TextColored(Accent, "2. DEFAULT DESTINATION");
            if (ImGui.BeginCombo("Server", store.GuildName.Length == 0 ? "Select a server" : store.GuildName))
            {
                foreach (var guild in guilds) if (ImGui.Selectable(guild.Name, store.GuildId == guild.Id))
                {
                    store.GuildId = guild.Id; store.GuildName = guild.Name; store.ChannelId = ""; store.ChannelName = ""; channels = [];
                    TrySaveSettings(); LoadChannels(guild.Id);
                }
                ImGui.EndCombo();
            }
            if (store.GuildId.Length > 0 && ImGui.Button("Refresh channels")) LoadChannels(store.GuildId);
            if (ImGui.BeginCombo("Announcement channel", store.ChannelName.Length == 0 ? "No announcement (event only)" : store.ChannelName))
            {
                if (ImGui.Selectable("No announcement (event only)", store.ChannelId == "")) { store.ChannelId = ""; store.ChannelName = ""; TrySaveSettings(); }
                foreach (var channel in channels) if (ImGui.Selectable("#" + channel.Name, channel.Id == store.ChannelId))
                { store.ChannelId = channel.Id; store.ChannelName = "#" + channel.Name; TrySaveSettings(); }
                ImGui.EndCombo();
            }
        }
        ImGui.Spacing(); DrawPlaySessionSettings();
    }
    private void LoadChannels(string guild) => Run(async () =>
    { var result = await relay.Send<DiscordChoice[]>(HttpMethod.Get, $"discord/guilds/{guild}/channels"); return () => { channels = result; message = "Channels refreshed."; }; });
    private void TrySaveSettings() { try { store.Save(); } catch (Exception ex) { message = "Could not save settings: " + ex.Message; } }
    private void Save(bool template)
    {
        if (EventRules.Validate(editor) is { } error) { message = error; return; }
        if (!template && editor.PersonalOnly && editor.DiscordRemindersEnabled) { SavePersonalWithReminders(); return; }
        var copy = editor.Copy(); copy.UpdatedUtc = DateTimeOffset.UtcNow;
        if (template) { copy.Id = Guid.NewGuid(); copy.Status = "Template"; copy.DiscordRemindersEnabled = false; copy.GoogleCalendarSync = false; copy.DiscordEventId = ""; copy.DiscordMessageId = ""; copy.RelayOrigin = ""; }
        else copy.Status = copy.PersonalOnly ? "Personal" : copy.DiscordEventId.Length > 0 ? "Published" : "Draft";
        try { store.Upsert(copy, template); editing = false; selected = copy.Id; section = template ? "Templates" : copy.Status == "Draft" ? "Drafts" : "Events"; message = copy.PersonalOnly && copy.GoogleCalendarSync ? "Saved locally. Use Save & sync my Google Calendar to send these changes to Google." : "Saved locally."; }
        catch (Exception ex) { message = "Save failed: " + ex.Message; }
    }
    private void Publish()
    {
        if (EventRules.Validate(editor, true) is { } error) { message = error; return; }
        if (editor.RelayOrigin.Length > 0 && editor.RelayOrigin != relay.Origin) { message = "Use the original relay to edit this published event, or create a copy."; return; }
        var copy = editor.Copy(); copy.UpdatedUtc = DateTimeOffset.UtcNow;
        // Persist the stable ID before any external operation, so retries address the same event.
        copy.RelayOrigin = relay.Origin;
        try { store.Upsert(copy); } catch (Exception ex) { message = "Save failed; nothing was published: " + ex.Message; return; }
        editor.RelayOrigin = copy.RelayOrigin;
        Run(async () =>
        {
            string? banner = copy.RemoveBanner ? "" : null;
            if (copy.BannerPath.Length > 0)
            {
                if (!File.Exists(copy.BannerPath)) throw new IOException("The selected banner is missing. Choose it again, or remove the banner before publishing.");
                if (new FileInfo(copy.BannerPath).Length > BannerUpload.MaxBytes) throw new ArgumentException("Choose a banner smaller than 4 MB.");
                banner = BannerUpload.Encode(await File.ReadAllBytesAsync(copy.BannerPath));
            }
            var outgoing = copy.Copy(); outgoing.BannerPath = "";
            var result = await relay.Send<PublishResult>(HttpMethod.Put, $"events/{copy.Id}/publish", new { Event = outgoing, BannerDataUrl = banner });
            return () =>
            {
                copy.DiscordEventId = result.EventId; copy.DiscordMessageId = result.MessageId; copy.RelayOrigin = relay.Origin; copy.Status = "Published";
                editor = copy;
                artworkKey = ""; artworkPath = "";
                try { store.Upsert(copy); editing = false; section = "Events"; selected = copy.Id; message = result.Warning ?? "Saved and synchronized with Discord."; }
                catch (Exception ex) { message = "Discord saved the event, but local saving failed. Keep this editor open and retry saving: " + ex.Message; }
            };
        });
    }
    private void Run(Func<Task<Action>> action) { if (!Busy) { message = ""; operation = Task.Run(action); } }
    private void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { message = "Could not open the browser: " + ex.Message; }
    }
    private void DrawIcon(float size)
    {
        var wrap = textures.GetFromFile(iconPath).GetWrapOrDefault();
        if (wrap is not null) ImGui.Image(wrap.Handle, new Vector2(size)); else ImGui.Dummy(new Vector2(size));
    }
    public void Dispose() { foreach (var file in previewFiles) try { File.Delete(file); } catch { } titleFont?.Dispose(); headingFont?.Dispose(); bodyFont?.Dispose(); relay.Dispose(); if (operation is { } task) _ = task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted); }
}
