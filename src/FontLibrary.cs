using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private sealed record ImportedFont(string Id, string Name, string Format);
    private ImportedFont[] importedFonts = [];
    private string fontSession = "";
    private string fontSearch = "";
    private string importFontPath = "";
    private string importFontName = "";
    private bool fontLicenseConfirmed;
    private static readonly (string Name, string Category)[] SharedFonts =
    [
        ("Sans", "Standard"), ("Cinzel", "Fantasy"), ("Serif", "Standard"), ("Mono", "Standard"),
        ("Orbitron", "Futuristic"), ("Rajdhani", "Futuristic"), ("Exo 2", "Futuristic"),
        ("Chakra Petch", "Futuristic"), ("Oxanium", "Futuristic"), ("Space Grotesk", "Modern"),
        ("Inter", "Readable"), ("Nunito Sans", "Readable"), ("Lora", "Literary"),
        ("Cormorant Garamond", "Fantasy"), ("Playfair Display", "Elegant"), ("Uncial Antiqua", "Fantasy"),
        ("Grenze Gotisch", "Gothic"), ("Great Vibes", "Script"), ("Dancing Script", "Script"),
        ("Caveat", "Handwritten"), ("JetBrains Mono", "Monospace"), ("Special Elite", "Typewriter"),
    ];
    private void DrawFontLibrary(TextAppearance style)
    {
        var session = relay.Origin + relay.Token;
        if (fontSession != session)
        {
            fontSession = session; importedFonts = []; importFontPath = ""; fontLicenseConfirmed = false;
        }
        var selected = importedFonts.FirstOrDefault(f => f.Id == style.Font)?.Name
            ?? (style.Font.StartsWith("custom-") ? "Imported font (refresh library for name)" : style.Font);
        ImGui.InputTextWithHint("##fontSearch", "Search fonts or styles", ref fontSearch, 100);
        if (ImGui.BeginCombo("Font family", selected))
        {
            foreach (var (name, category) in SharedFonts)
                if ((name + " " + category).Contains(fontSearch, StringComparison.OrdinalIgnoreCase)
                    && ImGui.Selectable($"{name} — {category}", name == style.Font)) style.Font = name;
            foreach (var font in importedFonts)
                if (font.Name.Contains(fontSearch, StringComparison.OrdinalIgnoreCase)
                    && ImGui.Selectable($"{font.Name} — Imported ({font.Format.ToUpperInvariant()})##{font.Id}", font.Id == style.Font)) style.Font = font.Id;
            ImGui.EndCombo();
        }
        ImGui.TextWrapped("Shared fonts include original bold and italic faces where available. Other combinations use simulated bold or slant. Render a preview to see the exact lettering.");
        if (!ImGui.TreeNode("Import custom font / My font library")) return;
        ImGui.TextWrapped("Import TTF, OTF, WOFF or WOFF2 files, up to 1 MB each (20 per Discord account). Fonts are stored on this relay so future announcement updates retain them. Imported fonts are for announcement images; they do not change the in-game interface font.");
        ImGui.BeginDisabled(!relay.Connected || Busy);
        if (ImGui.Button("Refresh my font library", new Vector2(240, 36))) Run(async () =>
        {
            var result = await relay.Send<ImportedFont[]>(HttpMethod.Get, "fonts");
            return () => { if (relay.Origin + relay.Token == session) importedFonts = result; message = "Your imported font library is ready."; };
        });
        if (ImGui.Button("Choose font file...", new Vector2(240, 36))) Run(async () =>
        {
            var path = await BannerPicker.Choose(font: true);
            if (path is null) return () => { };
            if (new FileInfo(path).Length > 1024 * 1024) throw new ArgumentException("Choose a font smaller than 1 MB.");
            return () => { importFontPath = path; importFontName = Path.GetFileNameWithoutExtension(path); fontLicenseConfirmed = false; };
        });
        if (importFontPath.Length > 0)
        {
            ImGui.TextWrapped(Path.GetFileName(importFontPath));
            ImGui.InputText("Font label", ref importFontName, 64);
            ImGui.Checkbox("This font's license allows uploading and embedding", ref fontLicenseConfirmed);
            ImGui.BeginDisabled(!fontLicenseConfirmed || string.IsNullOrWhiteSpace(importFontName));
            if (ImGui.Button("Upload and use font", new Vector2(240, 36)))
            {
                var path = importFontPath; var name = importFontName;
                Run(async () =>
                {
                    if (new FileInfo(path).Length > 1024 * 1024) throw new ArgumentException("Choose a font smaller than 1 MB.");
                    var bytes = await File.ReadAllBytesAsync(path);
                    var result = await relay.Send<ImportedFont>(HttpMethod.Post, "fonts", new { Name = name, Data = Convert.ToBase64String(bytes), LicenseConfirmed = true });
                    return () =>
                    {
                        if (relay.Origin + relay.Token != session) return;
                        importedFonts = importedFonts.Where(f => f.Id != result.Id).Append(result).ToArray();
                        style.Font = result.Id; importFontPath = ""; fontLicenseConfirmed = false;
                        message = "Font saved to your relay library and selected. Render a preview to check it.";
                    };
                });
            }
            ImGui.EndDisabled();
        }
        ImGui.EndDisabled();
        if (!relay.Connected) ImGui.TextWrapped("Connect Discord to import or restore your personal font library.");
        ImGui.TreePop();
    }
}
