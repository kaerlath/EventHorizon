using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private sealed record GoogleStatus(bool Configured, bool Connected, string Email, bool CalendarReady, int Pending, int Failed);
    private sealed record GoogleLink(string Code, string VerificationUrl);
    private sealed record GoogleDisconnect(string? Warning);
    private sealed record GoogleRetry(int Count);
    private GoogleStatus? googleStatus;
    private GoogleLink? googleLink;
    private string googleSession = "";
    private bool confirmGoogleDisconnect;

    private void RefreshGoogleStatus() => Run(async () =>
    {
        var status = await relay.Send<GoogleStatus>(HttpMethod.Get, "google/status");
        return () => { googleStatus = status; message = "Google Calendar status refreshed."; };
    });
    private void DrawGoogleSettings()
    {
        if (googleSession != relay.Token) { googleSession = relay.Token; googleStatus = null; googleLink = null; confirmGoogleDisconnect = false; }
        if (!ImGui.CollapsingHeader("Google Calendar (optional)")) return;
        if (ImGui.Button("Google Calendar walkthrough")) OpenHelp(6);
        ImGui.TextWrapped("Connect your own Google account to create a dedicated Event Horizon calendar. View any eligible event and choose Add to my Google Calendar. Google edits do not change Discord events.");
        if (!relay.Connected) { ImGui.TextDisabled("Connect Discord below first."); return; }
        if (ImGui.Button("Refresh Google status", new Vector2(230, 34))) RefreshGoogleStatus();
        if (googleStatus is null) return;
        if (!googleStatus.Configured) { ImGui.TextWrapped("The relay operator still needs to configure Google Calendar. Discord publishing remains available."); return; }
        if (!googleStatus.Connected)
        {
            if (ImGui.Button("Connect Google Calendar", new Vector2(250, 36))) Run(async () =>
            {
                var link = await relay.Send<GoogleLink>(HttpMethod.Post, "google/link");
                if (!Uri.TryCreate(link.VerificationUrl, UriKind.Absolute, out var uri) || uri.GetLeftPart(UriPartial.Authority) + "/" != relay.Origin)
                    throw new InvalidOperationException("The relay returned an unexpected authorization address.");
                return () => { googleLink = link; message = "Open Google authorization, verify the reference, then refresh Google status."; };
            });
            if (googleLink is { } link)
            {
                ImGui.TextUnformatted("Connection reference: " + link.Code);
                if (ImGui.Button("Open Google authorization")) OpenUrl(link.VerificationUrl);
            }
            return;
        }
        ImGui.TextColored(Accent, "Connected: " + googleStatus.Email);
        ImGui.TextWrapped(googleStatus.CalendarReady ? "Dedicated Event Horizon calendar ready." : "Your account is connected. Prepare the dedicated calendar next.");
        if (!googleStatus.CalendarReady && ImGui.Button("Prepare calendar", new Vector2(220, 34))) Run(async () =>
        {
            var result = await relay.Send<GoogleStatus>(HttpMethod.Post, "google/prepare");
            return () => { googleStatus = result; message = "Event Horizon calendar ready. Open an event and choose Add to my Google Calendar."; };
        });
        ImGui.TextWrapped($"Pending calendar updates: {googleStatus.Pending}  |  Updates needing attention: {googleStatus.Failed}");
        ImGui.TextWrapped("Updates run on the relay, even after you close the game. Failures retry automatically up to six times. Reconnect if Google access was revoked.");
        if (googleStatus.CalendarReady && ImGui.Button("Retry enabled event syncs")) Run(async () =>
        {
            var result = await relay.Send<GoogleRetry>(HttpMethod.Post, "google/retry");
            return () => message = $"Queued {result.Count} enabled events. Refresh status shortly.";
        });
        DrawPersonalSubscriptionList();
        if (ImGui.Button("Disconnect Google")) confirmGoogleDisconnect = true;
        if (confirmGoogleDisconnect)
        {
            ImGui.TextWrapped("Stop syncing and remove this relay's Google connection? Existing calendar entries remain.");
            if (ImGui.Button("Confirm disconnect")) Run(async () =>
            {
                var result = await relay.Send<GoogleDisconnect>(HttpMethod.Post, "google/disconnect");
                return () => { googleStatus = null; googleLink = null; confirmGoogleDisconnect = false; message = result.Warning ?? "Google disconnected. Existing calendar entries were kept."; };
            });
            ImGui.SameLine(); if (ImGui.Button("Keep connected")) confirmGoogleDisconnect = false;
        }
    }
}
