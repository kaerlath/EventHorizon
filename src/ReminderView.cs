using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private static readonly int[] ReminderLeads = [0, 5, 10, 15, 30, 60, 120, 1440];
    private static string LeadLabel(int minutes) => minutes == 0 ? "At event start" : minutes >= 60 ? $"{minutes / 60} hour(s) before" : $"{minutes} minutes before";
    private void ChangeReminder(Action<ReminderSettings> change)
    {
        try { store.ChangeReminders(change); }
        catch (Exception ex) { message = "Could not save reminder: " + ex.Message; }
    }
    private void DrawEventReminder(EventRecord item)
    {
        ImGui.Separator(); ImGui.TextColored(Accent, "PERSONAL REMINDER");
        if (ImGui.SmallButton("How do reminders work?")) OpenHelp(5);
        var reminder = store.Reminders.Items.FirstOrDefault(r => r.EventId == item.Id && r.RelayOrigin == item.RelayOrigin);
        var enabled = reminder is not null;
        if (ImGui.Checkbox("Remind me on screen", ref enabled))
        {
            ChangeReminder(settings =>
            {
                settings.Items.RemoveAll(r => r.EventId == item.Id && r.RelayOrigin == item.RelayOrigin);
                if (enabled)
                {
                    var entry = new EventReminder { EventId = item.Id, RelayOrigin = item.RelayOrigin };
                    entry.Refresh(item); settings.Items.Add(entry);
                }
            });
            reminder = store.Reminders.Items.FirstOrDefault(r => r.EventId == item.Id && r.RelayOrigin == item.RelayOrigin);
        }
        if (reminder is not null)
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##remindBefore", LeadLabel(reminder.MinutesBefore)))
            {
                foreach (var minutes in ReminderLeads)
                    if (ImGui.Selectable(LeadLabel(minutes), minutes == reminder.MinutesBefore))
                        ChangeReminder(_ => { reminder.MinutesBefore = minutes; reminder.DismissedStartUtc = null; reminder.SnoozedUntilUtc = null; });
                ImGui.EndCombo();
            }
            if (!store.Reminders.Enabled)
            {
                ImGui.TextWrapped("Reminder popups are currently turned off.");
                if (ImGui.Button("Enable reminder popups", new Vector2(-1, 36))) ChangeReminder(s => s.Enabled = true);
            }
        }
    }
    private void DrawReminderSettings()
    {
        Heading("Personal reminders");
        var enabled = store.Reminders.Enabled;
        if (ImGui.Checkbox("Enable on-screen event reminders", ref enabled)) ChangeReminder(s => s.Enabled = enabled);
        ImGui.TextWrapped("Reminders work while the Event Horizon window is closed, as long as the game and plugin are running. Choose Remind me on screen in an event's details. These preferences are personal to this plugin installation and do not change Discord or Google Calendar.");
        ImGui.TextWrapped("Reminders use your saved event schedule. Refresh community events to receive organizer changes. Popups stop ten minutes after the start, or when the event ends, whichever is first.");
        foreach (var reminder in store.Reminders.Items.ToArray())
        {
            ImGui.PushID(reminder.EventId + reminder.RelayOrigin);
            ImGui.Separator(); ImGui.TextWrapped(reminder.Title);
            ImGui.TextColored(Muted, $"{reminder.StartUtc.ToLocalTime():ddd, MMM d · h:mm tt} · {LeadLabel(reminder.MinutesBefore)}");
            if (ImGui.Button("Open event")) OpenReminder(reminder);
            ImGui.SameLine(); if (ImGui.Button("Remove reminder")) ChangeReminder(s => s.Items.Remove(reminder));
            ImGui.PopID();
        }
        if (store.Reminders.Items.Count == 0) ImGui.TextDisabled("No personal reminders yet.");
    }
    public void OpenReminder(EventReminder reminder)
    {
        IsOpen = true;
        Navigate(() =>
        {
            var item = Find(reminder.EventId);
            if (item is null || item.RelayOrigin != reminder.RelayOrigin) { section = "Reminders"; selected = null; message = "Refresh community events to load this event again."; return; }
            section = "Events"; selected = item.Id; editing = false;
        });
    }
    public void ProcessBackgroundWork()
    {
        ProcessPendingOperation();
        // Refresh reminders from locally available records without network polling.
        var records = EventLibrary().ToArray();
        foreach (var reminder in store.Reminders.Items.ToArray())
        {
            var item = records.FirstOrDefault(e => e.Id == reminder.EventId && (e.RelayOrigin == reminder.RelayOrigin || reminder.RelayOrigin.Length == 0));
            if (item is null) continue;
            try
            {
                if (item.Status is "Archived" or "Deleting" or "Deleted") { ChangeReminder(s => s.Items.Remove(reminder)); continue; }
                var updated = reminder.Copy(); updated.Refresh(item); updated.RelayOrigin = item.RelayOrigin;
                if (updated.StartUtc != reminder.StartUtc || updated.EndUtc != reminder.EndUtc || updated.Title != reminder.Title || updated.Location != reminder.Location || updated.RelayOrigin != reminder.RelayOrigin)
                    store.ChangeReminders(s => { var index = s.Items.IndexOf(reminder); s.Items[index] = updated; });
            }
            catch (Exception ex) { message = "Reminder schedule could not update: " + ex.Message; }
        }
    }
}

internal sealed class ReminderWindow : Window
{
    private readonly EventStore store;
    private readonly MainWindow main;
    private string error = "";
    public ReminderWindow(EventStore store, MainWindow main) : base("Event Horizon reminder###EventHorizonReminder")
    {
        this.store = store; this.main = main;
        Size = new Vector2(410, 280); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(350, 180), MaximumSize = new Vector2(700, 700) };
        ShowCloseButton = false;
    }
    public void UpdateVisibility() => IsOpen = store.LoadError is null && ReminderClock.Due(store.Reminders, DateTimeOffset.UtcNow).Length > 0;
    private void Change(Action<ReminderSettings> action)
    {
        try { store.ChangeReminders(action); error = ""; } catch (Exception ex) { error = ex.Message; }
    }
    public override void Draw()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var reminder in ReminderClock.Due(store.Reminders, now))
        {
            ImGui.PushID(reminder.EventId + reminder.RelayOrigin);
            ImGui.TextColored(new Vector4(.72f, .72f, 1, 1), ReminderClock.Countdown(reminder.StartUtc, now));
            ImGui.TextWrapped(reminder.Title); ImGui.TextWrapped(reminder.Location);
            if (ImGui.Button("Open event")) main.OpenReminder(reminder);
            ImGui.SameLine();
            if (ImGui.Button("Dismiss")) Change(_ => reminder.DismissedStartUtc = reminder.StartUtc);
            if (ImGui.Button("Snooze 5 minutes")) Change(_ => reminder.SnoozedUntilUtc = now.AddMinutes(5));
            ImGui.Separator(); ImGui.PopID();
        }
        if (ImGui.Button("Turn off all reminder popups")) Change(s => s.Enabled = false);
        if (error.Length > 0) ImGui.TextWrapped("Could not save reminder preference: " + error);
    }
}
