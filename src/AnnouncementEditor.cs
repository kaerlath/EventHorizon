using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private int textStyleTarget;
    private string? renderedPreviewPath;
    private bool showRenderedPreview;
    private readonly List<string> previewFiles = new();
    private sealed record CardPreview(string ImageBase64);

    private void DrawAnnouncementStyles()
    {
        if (!ImGui.CollapsingHeader("Announcement typography & effects")) return;
        editor.AnnouncementStyle ??= new();
        var design = editor.AnnouncementStyle;
        var paragraphs = Regex.Split(editor.Description.Replace("\r\n", "\n"), @"\n\s*\n");
        textStyleTarget = Math.Clamp(textStyleTarget, 0, paragraphs.Length + 1);
        string Target(int i) => i == 0 ? "Title" : i == 1 ? "Description default" : $"Paragraph {i - 1}";
        if (ImGui.BeginCombo("Style", Target(textStyleTarget)))
        {
            for (var i = 0; i < paragraphs.Length + 2; i++) if (ImGui.Selectable(Target(i), textStyleTarget == i)) textStyleTarget = i;
            ImGui.EndCombo();
        }
        var paragraph = textStyleTarget - 2;
        if (paragraph >= 0)
        {
            ImGui.TextWrapped(paragraphs[paragraph]);
            var custom = design.Paragraphs.ContainsKey(paragraph);
            if (ImGui.Checkbox("Customize this paragraph", ref custom))
            {
                if (custom && design.Paragraphs.Count >= 64) { message = "Use up to 64 paragraph overrides."; custom = false; }
                else if (custom) design.Paragraphs[paragraph] = design.Body.Copy();
                else design.Paragraphs.Remove(paragraph);
            }
            ImGui.TextWrapped("Separate paragraphs with a blank line. Overrides follow paragraph positions when text is edited.");
            if (!custom) { ImGui.TextDisabled("This paragraph uses the description default."); return; }
        }
        var style = textStyleTarget == 0 ? design.Title : textStyleTarget == 1 ? design.Body : design.Paragraphs[paragraph];
        DrawFontLibrary(style);
        var size = style.Size; if (ImGui.SliderInt("Text size", ref size, 16, 64, "%d px")) style.Size = size;
        var bold = style.Bold; if (ImGui.Checkbox("Bold", ref bold)) style.Bold = bold;
        ImGui.SameLine(); var italic = style.Italic; if (ImGui.Checkbox("Italic", ref italic)) style.Italic = italic;
        ImGui.SameLine(); var underline = style.Underline; if (ImGui.Checkbox("Underline", ref underline)) style.Underline = underline;
        ImGui.SameLine(); var strike = style.Strike; if (ImGui.Checkbox("Strikethrough", ref strike)) style.Strike = strike;
        EditColor("Text color", style.Color, x => style.Color = x);
        if (ImGui.BeginCombo("Alignment", style.Align))
        {
            foreach (var name in new[] { "left", "center", "right" }) if (ImGui.Selectable(name, name == style.Align)) style.Align = name;
            ImGui.EndCombo();
        }
        var outline = style.Outline; if (ImGui.SliderFloat("Outline width", ref outline, 0, 3, "%.1f px")) style.Outline = outline;
        EditColor("Outline color", style.OutlineColor, x => style.OutlineColor = x);
        var glow = style.Glow; if (ImGui.SliderFloat("Text glow", ref glow, 0, 24, "%.0f px")) style.Glow = glow;
        EditColor("Glow color", style.GlowColor, x => style.GlowColor = x);
        var spacing = style.LetterSpacing; if (ImGui.SliderFloat("Letter spacing", ref spacing, 0, 6, "%.1f px")) style.LetterSpacing = spacing;
        var line = style.LineHeight; if (ImGui.SliderFloat("Line spacing", ref line, 1, 2, "%.2f")) style.LineHeight = line;
        if (ImGui.Button("Reset this text style"))
        {
            if (textStyleTarget == 0) design.Title = new() { Size = 38, Bold = true };
            else if (textStyleTarget == 1) design.Body = new();
            else design.Paragraphs.Remove(paragraph);
        }
        ImGui.TextWrapped("These effects appear in the composed announcement image. The native Discord event keeps plain text. Use the rendered preview below to check the result.");
    }

    private void RequestStyledPreview()
    {
        var item = editor.Copy(); item.BannerPath = "";
        var path = editor.BannerPath; var remove = editor.RemoveBanner;
        Run(async () =>
        {
            string? image = remove ? "" : null;
            if (path.Length > 0)
            {
                if (new FileInfo(path).Length > BannerUpload.MaxBytes) throw new ArgumentException("Choose an image smaller than 4 MB.");
                image = BannerUpload.Encode(await File.ReadAllBytesAsync(path));
            }
            var result = await relay.Send<CardPreview>(HttpMethod.Post, "preview", new { Event = item, BannerDataUrl = image });
            var bytes = Convert.FromBase64String(result.ImageBase64);
            _ = BannerUpload.Encode(bytes);
            var output = Path.Combine(Path.GetTempPath(), "event-horizon-preview-" + Guid.NewGuid() + ".png");
            await File.WriteAllBytesAsync(output, bytes);
            return () => { previewFiles.Add(output); renderedPreviewPath = output; showRenderedPreview = true; message = "Rendered preview ready. Nothing was posted to Discord."; };
        });
    }
    private void DrawRenderedPreview()
    {
        if (!showRenderedPreview || renderedPreviewPath is null) return;
        ImGui.SetNextWindowSize(new Vector2(850, 750), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Announcement image preview", ref showRenderedPreview))
        {
            ImGui.TextWrapped("Snapshot of the event when you clicked Render preview. Render again after further edits.");
            var image = textures.GetFromFile(renderedPreviewPath).GetWrapOrDefault();
            if (image is not null)
            {
                var width = Math.Max(100, ImGui.GetContentRegionAvail().X);
                ImGui.Image(image.Handle, new Vector2(width, width * image.Height / image.Width));
            }
        }
        ImGui.End();
    }
}
