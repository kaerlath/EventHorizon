using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private EventRecord[] communityEvents = [];
    private string communitySession = "";
    private sealed record PersonalSubscription(Guid Id, string Title, string Status, bool Active);
    private sealed record SubscriptionResult(string Message);
    private PersonalSubscription[] personalSubscriptions = [];

    private void ResetCommunitySession()
    {
        var key = relay.Origin + relay.Token;
        if (key == communitySession) return;
        communitySession = key; communityEvents = []; personalSubscriptions = [];
    }
    private IEnumerable<EventRecord> EventLibrary()
    {
        // Keep unsynchronized local edits for your own events. Community copies
        // belong to this authenticated session and are never saved as local drafts.
        foreach (var item in store.Events)
            yield return communityEvents.FirstOrDefault(e => e.Id == item.Id && e.ReadOnly) ?? item;
        foreach (var item in communityEvents.Where(e => store.Events.All(local => local.Id != e.Id))) yield return item;
    }
    private bool IsReadOnly(EventRecord item) => item.ReadOnly || communityEvents.Any(e => e.Id == item.Id && e.ReadOnly);
    private void DrawCommunityToolbar()
    {
        ImGui.BeginDisabled(!relay.Connected);
        if (ImGui.Button("Refresh community events", new Vector2(245, 34)))
        {
            communityEvents = [];
            Run(async () =>
            {
                var result = await relay.Send<EventRecord[]>(HttpMethod.Get, "events/community");
                return () => { communityEvents = result; message = $"Loaded {result.Length} visible Event Horizon events. Select a day and view an event to add it to your calendar."; };
            });
        }
        ImGui.EndDisabled();
        ImGui.TextWrapped(relay.Connected ? "Community events follow your Discord server and channel access. Only the publisher can edit the original." : "Connect Discord in Settings to browse community events.");
    }
    private void ChangePersonalSubscription(Guid id, bool remove)
    {
        Run(async () =>
        {
            var response = await relay.Send<SubscriptionResult>(remove ? HttpMethod.Delete : HttpMethod.Post, "google/subscriptions/" + id);
            return () =>
            {
                // This replaces the legacy organizer checkbox for your own copy.
                // Persist only that preference, preserving unsaved local event details.
                if (store.Events.FirstOrDefault(e => e.Id == id) is { } local && local.GoogleCalendarSync)
                {
                    var copy = local.Copy(); copy.GoogleCalendarSync = false;
                    try { store.Upsert(copy); } catch { /* Relay preference is authoritative. */ }
                }
                personalSubscriptions = []; message = response.Message;
            };
        });
    }
    private void DrawPersonalCalendarActions(EventRecord item)
    {
        ImGui.Separator(); ImGui.TextColored(Accent, "MY GOOGLE CALENDAR");
        ImGui.TextWrapped("Subscribe to organizer updates without changing the shared event.");
        var sameRelay = item.RelayOrigin.Length == 0 || item.RelayOrigin == relay.Origin;
        ImGui.BeginDisabled(!relay.Connected || !sameRelay);
        if (ImGui.Button("Add to my Google Calendar", new Vector2(-1, 38))) ChangePersonalSubscription(item.Id, false);
        if (ImGui.Button("Remove from my calendar", new Vector2(-1, 34))) ChangePersonalSubscription(item.Id, true);
        ImGui.EndDisabled();
        if (!sameRelay) ImGui.TextWrapped("Connect to this event's original relay to manage its calendar subscription.");
        ImGui.TextWrapped("Removal affects only your Google copy. Calendar connection and subscription status are in Settings.");
        ImGui.Separator();
    }
    private void DrawPersonalSubscriptionList()
    {
        if (ImGui.Button("Refresh my subscriptions")) Run(async () =>
        {
            var result = await relay.Send<PersonalSubscription[]>(HttpMethod.Get, "google/subscriptions");
            return () => { personalSubscriptions = result; message = $"Loaded {result.Length} personal calendar subscriptions."; };
        });
        foreach (var sub in personalSubscriptions)
        {
            ImGui.PushID(sub.Id.ToString());
            ImGui.TextWrapped(sub.Title + " · " + sub.Status);
            if (ImGui.SmallButton("Remove my copy")) ChangePersonalSubscription(sub.Id, true);
            if (sub.Status != "Removing")
            {
                ImGui.SameLine(); if (ImGui.SmallButton("Resume / retry")) ChangePersonalSubscription(sub.Id, false);
            }
            ImGui.PopID();
        }
    }
}
