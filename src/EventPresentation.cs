using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private sealed record EventArtwork(string ImageBase64, bool Published, string CreatedUtc);
    private string artworkKey = "", artworkPath = "", artworkCaption = "";
    private bool showAnnouncement = true;

    private void ConnectionBadge(bool compact = false)
    {
        var connected = relay.Connected;
        var color = connected ? new Vector4(.32f, .95f, .57f, 1) : Muted;
        var p = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();
        if (connected && store.Appearance.Glows) draw.AddCircleFilled(p + new Vector2(7, 9), 9, Color(new Vector4(color.X, color.Y, color.Z, .14f)));
        draw.AddCircleFilled(p + new Vector2(7, 9), 4, Color(color));
        ImGui.SetCursorScreenPos(p + new Vector2(22, 0));
        ImGui.TextColored(color, connected ? (compact ? "Connected" : "Connected to Discord") : "Not connected");
    }

    private void PanelBackdrop()
    {
        var p = ImGui.GetWindowPos(); var size = ImGui.GetWindowSize();
        ImGui.GetWindowDrawList().AddRectFilledMultiColor(p, p + size,
            Color(BackgroundSurface(.12f)), Color(BackgroundSurface(.035f)), Color(BackgroundSurface(.02f)), Color(BackgroundSurface(.055f)));
    }

    private bool ActionButton(string text, FontAwesomeIcon icon, float width = 150, bool primary = false)
    {
        var p = ImGui.GetCursorScreenPos();
        if (primary) ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Lerp(new Vector4(.15f, .17f, .55f, 1), Accent, .32f));
        var clicked = ImGui.Button("##action" + text, new Vector2(width, 40));
        var max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddRect(p, max, Color(Surface(.5f)), 6);
        using (navIconFont?.Push()) ImGui.GetWindowDrawList().AddText(p + new Vector2(13, 12), Color(Accent), char.ConvertFromUtf32((int)icon));
        ImGui.GetWindowDrawList().AddText(p + new Vector2(38, 11), ImGui.GetColorU32(ImGuiCol.Text), text);
        if (primary) ImGui.PopStyleColor();
        return clicked;
    }

    private bool DrawEventArtwork(EventRecord item)
    {
        var key = relay.Origin + relay.Token + item.Id;
        if (artworkKey != key) { artworkPath = ""; artworkCaption = ""; }
        var eligible = relay.Connected && item.DiscordEventId.Length > 0 && (item.RelayOrigin.Length == 0 || item.RelayOrigin == relay.Origin);
        if (eligible && artworkKey != key && !Busy) RequestEventArtwork(item, key);
        if (!eligible) { ImGui.TextWrapped("Connect to this event's relay to view its announcement image."); return false; }
        if (artworkPath.Length == 0)
        {
            ImGui.TextColored(Muted, Busy ? "Loading announcement artwork…" : "Announcement artwork is not available. The event details are shown below.");
            if (!Busy && ImGui.Button("Retry loading artwork")) RequestEventArtwork(item, key);
            return false;
        }
        ImGui.TextWrapped(artworkCaption);
        if (ImGui.Button("Refresh artwork")) RequestEventArtwork(item, key);
        var image = textures.GetFromFile(artworkPath).GetWrapOrDefault();
        if (image is null) { ImGui.TextDisabled("Preparing image…"); return true; }
        var width = Math.Min(1100, ImGui.GetContentRegionAvail().X);
        ImGui.Image(image.Handle, new Vector2(width, width * image.Height / image.Width));
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Click to enlarge the announcement");
        if (ImGui.IsItemClicked()) { renderedPreviewPath = artworkPath; showRenderedPreview = true; }
        return true;
    }

    private void RequestEventArtwork(EventRecord item, string key)
    {
        artworkKey = key;
        Run(async () =>
        {
            var result = await relay.Send<EventArtwork>(HttpMethod.Get, $"events/{item.Id}/image");
            var bytes = Convert.FromBase64String(result.ImageBase64); _ = BannerUpload.Encode(bytes);
            var path = Path.Combine(Path.GetTempPath(), "event-horizon-art-" + Guid.NewGuid() + ".png");
            await File.WriteAllBytesAsync(path, bytes);
            return () =>
            {
                previewFiles.Add(path);
                if (relay.Origin + relay.Token + item.Id != key) return;
                artworkPath = path;
                artworkCaption = result.Published ? "Last published announcement · local edits appear after Save & Sync" : "Preview of saved event · reconstructed for this older publication";
            };
        });
    }
}
