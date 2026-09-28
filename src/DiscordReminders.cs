using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private Guid dmSelected;
    private int dmAdvance = 60, dmNear = 15;
    private string dmStatus = "";
    private sealed record DmStatus(bool Enabled, string Message);
    private static object DmPayload(EventRecord item, bool enabled) => new
    {
        enabled, minutes = new[] { item.DiscordAdvanceMinutes, item.DiscordNearMinutes },
        @event = new { id = item.Id, title = item.Title, startLocal = item.StartLocal,
            timeZoneId = item.TimeZoneId, durationMinutes = item.DurationMinutes, personalOnly = true }
    };

    private void DrawDiscordReminders(EventRecord item)
    {
        if (!ImGui.CollapsingHeader("Discord DM reminders (optional)")) return;
        if (dmSelected != item.Id) { dmSelected = item.Id; dmAdvance = item.DiscordAdvanceMinutes; dmNear = item.DiscordNearMinutes; dmStatus = ""; }
        ImGui.TextWrapped("Off by default. The bot privately messages your linked Discord account at the two selected times, even when the game is closed. Delivery is checked about once a minute. Allow bot DMs and share a server with the bot.");
        ImGui.BeginDisabled(!relay.Connected);
        var enabled = item.DiscordRemindersEnabled;
        if (ImGui.Checkbox("Send me Discord reminders", ref enabled)) UpdateDmReminders(item, enabled);
        if (ImGui.BeginCombo("Advance reminder", LeadLabel(dmAdvance)))
        {
            foreach (var value in new[] { 15, 30, 60, 120, 1440, 10080 })
                if (ImGui.Selectable(LeadLabel(value), dmAdvance == value)) dmAdvance = value;
            ImGui.EndCombo();
        }
        if (ImGui.BeginCombo("Near-start reminder", LeadLabel(dmNear)))
        {
            foreach (var value in new[] { 0, 5, 10, 15, 30, 60 })
                if (ImGui.Selectable(LeadLabel(value), dmNear == value)) dmNear = value;
            ImGui.EndCombo();
        }
        if (enabled && ImGui.Button("Apply DM reminder times", new Vector2(-1, 34))) UpdateDmReminders(item, true);
        if (ImGui.Button("Refresh DM delivery status", new Vector2(-1, 34))) Run(async () =>
        {
            var status = await relay.Send<DmStatus>(HttpMethod.Get, $"events/{item.Id}/personal-reminders");
            return () => { dmStatus = status.Message; };
        });
        if (ImGui.Button("Send test reminder to me", new Vector2(-1, 34))) Run(async () =>
        {
            var result = await relay.Send<SubscriptionResult>(HttpMethod.Post, "discord/reminders/test");
            return () => message = result.Message;
        });
        ImGui.EndDisabled();
        if (!relay.Connected) ImGui.TextWrapped("Connect your Discord account to enable or cancel remote reminders.");
        ImGui.TextWrapped("Use the same linked account to change or cancel reminders. Match the near-start time to your on-screen reminder if you want both together. Equal times send one message. Past times are skipped.");
        if (enabled) ImGui.TextWrapped("Turn off Discord reminders before archiving. Saving edits also updates the bot schedule and requires a connection.");
        if (dmStatus.Length > 0) ImGui.TextWrapped(dmStatus);
    }

    private void UpdateDmReminders(EventRecord item, bool enabled)
    {
        var copy = item.Copy(); copy.DiscordAdvanceMinutes = dmAdvance; copy.DiscordNearMinutes = dmNear;
        SaveDm(copy, enabled);
    }
    private void SavePersonalWithReminders() => SaveDm(editor.Copy(), true);
    private void SaveDm(EventRecord copy, bool enabled)
    {
        if (!relay.Connected) { message = "Connect Discord to update or cancel the existing bot reminders before saving."; return; }
        if (copy.RelayOrigin.Length > 0 && copy.RelayOrigin != relay.Origin) { message = "Connect to the original relay to change these reminders."; return; }
        if (enabled && EventRules.Validate(copy) is { } error) { message = error; return; }
        var origin = relay.Origin;
        Run(async () =>
        {
            var result = await relay.Send<DmStatus>(HttpMethod.Put, $"events/{copy.Id}/personal-reminders", DmPayload(copy, enabled));
            return () =>
            {
                copy.DiscordRemindersEnabled = result.Enabled; copy.RelayOrigin = origin; copy.Status = "Personal";
                try { store.Upsert(copy); editing = false; section = "Events"; selected = copy.Id; dmStatus = result.Message; message = result.Message + (copy.GoogleCalendarSync ? " Google is unchanged; use Save & sync my Google Calendar to update it." : ""); }
                catch (Exception ex) { message = "Bot schedule changed, but local saving failed: " + ex.Message; }
            };
        });
    }
}
