using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private void DrawVisibilityChoice()
    {
        var locked = editor.DiscordEventId.Length > 0 || editor.RelayOrigin.Length > 0;
        ImGui.BeginDisabled(locked);
        if (ImGui.BeginCombo("Visibility", editor.PersonalOnly ? "Personal — only me" : "Community — publish to Discord"))
        {
            if (ImGui.Selectable("Personal — only me", editor.PersonalOnly))
            {
                editor.PersonalOnly = true;
                editor.GuildId = editor.ChannelId = editor.Server = editor.Channel = "";
                editor.Recurrence = "None";
            }
            if (ImGui.Selectable("Community — publish to Discord", !editor.PersonalOnly)) editor.PersonalOnly = false;
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
        if (locked) ImGui.TextWrapped("Make a copy to change visibility. Existing Discord or Google copies are not removed by duplication.");
        if (editor.PersonalOnly)
            ImGui.TextWrapped("Only in your local calendar. Optional Google sync sends details through the relay to your account, never to Discord. Google calendar sharing still applies. After saving, enable Personal Reminder in View for on-screen notifications.");
    }

    private void DrawPersonalSaveButton()
    {
        ImGui.BeginDisabled(!relay.Connected);
        if (ImGui.Button("Save & sync my Google Calendar", new Vector2(310, 34))) SyncPrivateEvent(editor, false);
        ImGui.EndDisabled();
        if (!relay.Connected) ImGui.TextWrapped("Save locally now, or connect your account in Settings for Google sync.");
    }

    private void DrawPrivateCalendarActions(EventRecord item)
    {
        if (!ImGui.CollapsingHeader("My Google Calendar")) return;
        ImGui.TextWrapped("Optional private copy in your connected Google Calendar. Your calendar sharing settings still apply. Local reminders are separate.");
        ImGui.BeginDisabled(!relay.Connected);
        if (ImGui.Button("Add / update my Google copy", new Vector2(-1, 36))) SyncPrivateEvent(item, false);
        if (item.RelayOrigin.Length > 0 && ImGui.Button("Remove my Google copy", new Vector2(-1, 36))) SyncPrivateEvent(item, true);
        ImGui.EndDisabled();
        ImGui.TextWrapped("Archiving locally keeps the Google copy. Remove it here first if you no longer want it.");
    }

    private void SyncPrivateEvent(EventRecord item, bool remove)
    {
        if (!item.PersonalOnly || item.DiscordEventId.Length > 0) { message = "This action is only for personal events."; return; }
        if (!remove && EventRules.Validate(item) is { } error) { message = error; return; }
        if (item.RelayOrigin.Length > 0 && item.RelayOrigin != relay.Origin) { message = "Connect to the original relay for this Google copy."; return; }
        var copy = item.Copy(); copy.Status = "Personal"; copy.UpdatedUtc = DateTimeOffset.UtcNow;
        // Save before sending; retries use the same event ID.
        try { store.Upsert(copy); } catch (Exception ex) { message = "Nothing sent: " + ex.Message; return; }
        var origin = relay.Origin;
        Run(async () =>
        {
            // Only the fields needed by Google leave this installation.
            var payload = new { copy.Id, copy.Title, copy.Description, copy.StartLocal, copy.TimeZoneId,
                copy.DurationMinutes, copy.ScheduleMode, copy.EndLocal, copy.Sessions, copy.EventType, copy.Tags, copy.Location, copy.World, PersonalOnly = true };
            if (copy.DiscordRemindersEnabled && !remove)
                await relay.Send<DmStatus>(HttpMethod.Put, $"events/{copy.Id}/personal-reminders", DmPayload(copy, true));
            var result = await relay.Send<SubscriptionResult>(remove ? HttpMethod.Delete : HttpMethod.Put,
                "google/personal/" + copy.Id, remove ? null : payload);
            return () =>
            {
                copy.GoogleCalendarSync = !remove; copy.RelayOrigin = origin;
                try { store.Upsert(copy); editing = false; selected = copy.Id; section = "Events"; message = result.Message; }
                catch (Exception ex) { message = "Google operation queued, but local saving failed: " + ex.Message; }
            };
        });
    }
}
