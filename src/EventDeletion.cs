using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private EventRecord? deleteCandidate;
    private sealed record DeletionResult(string Status, int PendingGoogle, string Message);
    internal bool ConfirmationPending => afterDiscard is not null || deleteCandidate is not null;
    internal void DrawConfirmation()
    {
        if (afterDiscard is not null)
        {
            ImGui.TextWrapped("Leave this editor without saving your changes?");
            if (ImGui.Button("Discard changes", new Vector2(180, 38)))
            {
                var next = afterDiscard; afterDiscard = null; editing = false; next();
            }
            ImGui.SameLine();
            if (ImGui.Button("Keep editing", new Vector2(160, 38))) afterDiscard = null;
            return;
        }
        if (deleteCandidate is not { } candidate) return;
        ImGui.TextWrapped("Delete: " + candidate.Title);
        ImGui.TextWrapped("This removes the Discord event and its announcement, hides it from community listings, and queues removal of Google copies managed by Event Horizon for you and other subscribers. This cannot be undone.");
        ImGui.TextWrapped("Disconnected Google accounts need to reconnect for removal. Independently copied/exported calendar entries cannot be removed. A deletion record remains in History so you can check or retry cleanup.");
        ImGui.BeginDisabled(Busy);
        if (ImGui.Button("Delete everywhere", new Vector2(190, 38)))
        {
            deleteCandidate = null; DeletePublishedEvent(candidate);
        }
        ImGui.SameLine();
        if (ImGui.Button("Keep event", new Vector2(150, 38))) deleteCandidate = null;
        ImGui.EndDisabled();
    }

    private void DrawDeletionControls(EventRecord item)
    {
        // Never authorize from the organizer display name. The relay checks publisher ID.
        if (item.PersonalOnly || item.DiscordEventId.Length == 0 || item.ReadOnly || communityEvents.Any(e => e.Id == item.Id && e.ReadOnly)) return;
        ImGui.Separator();
        var originalRelay = item.RelayOrigin.Length == 0 || item.RelayOrigin == relay.Origin;
        ImGui.BeginDisabled(!relay.Connected || !originalRelay);
        if (item.Status is "Deleting" or "Deleted")
        {
            if (ImGui.Button("Check deletion status", new Vector2(-1, 36))) DeletePublishedEvent(item, true);
            if (ImGui.Button("Retry remaining cleanup", new Vector2(-1, 36))) DeletePublishedEvent(item);
        }
        else if (ImGui.Button("Delete published event...", new Vector2(-1, 36))) deleteCandidate = item.Copy();
        ImGui.EndDisabled();
        if (!relay.Connected) ImGui.TextWrapped("Connect the publishing Discord account to delete this event.");
        if (!originalRelay) ImGui.TextWrapped("Use the original relay to delete this event.");
    }
    private void DeletePublishedEvent(EventRecord item, bool statusOnly = false)
    {
        var copy = item.Copy();
        Run(async () =>
        {
            var result = await relay.Send<DeletionResult>(statusOnly ? HttpMethod.Get : HttpMethod.Delete,
                $"events/{copy.Id}" + (statusOnly ? "/deletion" : ""));
            return () =>
            {
                copy.Status = result.Status;
                try
                {
                    store.Upsert(copy);
                    store.ChangeReminders(s => s.Items.RemoveAll(r => r.EventId == copy.Id && r.RelayOrigin == copy.RelayOrigin));
                    communityEvents = communityEvents.Where(e => e.Id != copy.Id).ToArray();
                    editing = false; selected = copy.Id; section = "History";
                    artworkKey = ""; artworkPath = ""; message = result.Message;
                }
                catch (Exception ex) { message = result.Message + " Local saving failed: " + ex.Message; }
            };
        });
    }
}

internal sealed class EventConfirmationWindow : Window
{
    private readonly MainWindow main;
    internal EventConfirmationWindow(MainWindow main) : base("Event Horizon confirmation###EventHorizonConfirmation",
        ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
    {
        this.main = main; ShowCloseButton = false;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(440, 110), MaximumSize = new Vector2(520, 550) };
    }
    internal void UpdateVisibility() => IsOpen = main.ConfirmationPending;
    public override void PreDraw() => ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2, ImGuiCond.Appearing, new Vector2(.5f));
    public override void Draw() => main.DrawConfirmation();
}
