using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private static readonly string[] ThemeNames = ["Event Horizon", "Nebula", "Aurora", "Solar", "Glacier"];
    private Vector4 Accent => ParseColor(store.Appearance.Accent, ThemeAccent);
    private Vector4 ThemeAccent => store.Appearance.Theme switch
    {
        "Nebula" => new(.88f, .48f, 1, 1), "Aurora" => new(.32f, .94f, .71f, 1),
        "Solar" => new(1, .73f, .32f, 1), "Glacier" => new(.35f, .82f, 1, 1),
        _ => new(.68f, .66f, 1, 1)
    };
    private Vector4 Surface(float amount) => Vector4.Lerp(new Vector4(.025f, .035f, .065f, 1), Accent, amount);
    private float BackgroundOpacity => float.IsFinite(store.Appearance.BackgroundOpacity) ? Math.Clamp(store.Appearance.BackgroundOpacity, .1f, 1) : 1;
    private Vector4 BackgroundSurface(float amount)
    {
        var color = Surface(amount);
        // Main background + custom panel/header are two composited layers.
        color.W = 1 - MathF.Sqrt(1 - BackgroundOpacity);
        return color;
    }
    private static uint Color(Vector4 color) => ImGui.ColorConvertFloat4ToU32(color);
    private static Vector4 ParseColor(string? hex, Vector4 fallback)
    {
        if (hex?.Length == 7 && hex[0] == '#' && uint.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return new Vector4((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f, 1);
        return fallback;
    }
    private static string Hex(Vector3 color) => $"#{(int)(Math.Clamp(color.X, 0, 1) * 255):X2}{(int)(Math.Clamp(color.Y, 0, 1) * 255):X2}{(int)(Math.Clamp(color.Z, 0, 1) * 255):X2}";
    private static bool EditColor(string label, string value, Action<string> set)
    {
        var color = ParseColor(value, Vector4.One); var rgb = new Vector3(color.X, color.Y, color.Z);
        if (!ImGui.ColorEdit3(label, ref rgb, ImGuiColorEditFlags.NoInputs)) return false;
        set(Hex(rgb)); return true;
    }
    private void DrawAppearance()
    {
        Heading("Appearance");
        ImGui.TextWrapped("Make this window your own. These preferences stay on this computer; announcement styling is saved with each event.");
        ImGui.Spacing();
        var settings = store.Appearance; bool changed = false;
        if (ImGui.BeginCombo("Theme", settings.Theme))
        {
            foreach (var theme in ThemeNames) if (ImGui.Selectable(theme, theme == settings.Theme))
            { settings.Theme = theme; settings.Accent = ""; changed = true; }
            ImGui.EndCombo();
        }
        changed |= EditColor("Accent color", Hex(new Vector3(Accent.X, Accent.Y, Accent.Z)), x => settings.Accent = x);
        var opacity = BackgroundOpacity * 100;
        if (ImGui.SliderFloat("Window background opacity", ref opacity, 10, 100, "%.0f%%")) { settings.BackgroundOpacity = opacity / 100; changed = true; }
        ImGui.TextWrapped("Lower this to see the game through the window. Text, buttons and event images stay readable. This does not change your Discord artwork.");
        var glows = settings.Glows; if (ImGui.Checkbox("Luminous borders", ref glows)) { settings.Glows = glows; changed = true; }
        var motion = settings.ReduceMotion; if (ImGui.Checkbox("Reduce motion", ref motion)) { settings.ReduceMotion = motion; changed = true; }
        ImGui.TextWrapped("Reduced motion keeps a steady highlight and stops the rotating gravity drive.");
        var strength = settings.GlowStrength;
        if (ImGui.SliderFloat("Glow intensity", ref strength, 0, 1, "%.2f")) { settings.GlowStrength = strength; changed = true; }
        var speed = settings.AnimationSpeed;
        if (ImGui.SliderFloat("Animation speed", ref speed, .25f, 2, "%.2fx")) { settings.AnimationSpeed = speed; changed = true; }
        var height = settings.BannerHeight;
        if (ImGui.SliderFloat("In-game banner height", ref height, 240, 600, "%.0f px")) { settings.BannerHeight = height; changed = true; }
        ImGui.Spacing(); Heading("Calendar fonts");
        var calendarScale = CalendarScale(settings.CalendarFontScale);
        if (ImGui.SliderFloat("Calendar event text size", ref calendarScale, .7f, 1.5f, "%.2fx")) { settings.CalendarFontScale = calendarScale; changed = true; }
        ImGui.TextWrapped("Calendar text size controls the event capsules. Category colors stay consistent; titles and full details appear below the calendar. Announcement typography is separate.");
        if (ImGui.Button("Restore defaults")) { store.Appearance = new(); changed = true; }
        if (changed) TrySaveSettings();
        ImGui.Spacing(); Heading("Live preview");
        var top = ImGui.GetCursorScreenPos(); var size = new Vector2(Math.Min(440, ImGui.GetContentRegionAvail().X), 130);
        ImGui.GetWindowDrawList().AddRectFilled(top, top + size, Color(Surface(.12f)), 8);
        DrawFrameLight(top + new Vector2(4), top + size - new Vector2(4));
        ImGui.SetCursorScreenPos(top + new Vector2(16)); DrawOrb(96);
        ImGui.SetCursorScreenPos(top + new Vector2(126, 40)); ImGui.TextColored(Accent, "Your next gathering");
        ImGui.SetCursorScreenPos(top + new Vector2(126, 67)); ImGui.TextUnformatted("A little light in the void.");
        ImGui.SetCursorScreenPos(top); ImGui.Dummy(size);
    }

    // Same layered, travelling perimeter-light approach as Calliope's portrait frame.
    private void DrawFrameLight(Vector2 minimum, Vector2 maximum)
    {
        var settings = store.Appearance;
        if (!settings.Glows || maximum.X <= minimum.X || maximum.Y <= minimum.Y) return;
        var draw = ImGui.GetWindowDrawList();
        var head = settings.ReduceMotion ? .16f : (float)(ImGui.GetTime() * .075 * Math.Clamp(settings.AnimationSpeed, .25f, 2) % 1);
        for (var i = 0; i < 96; i++)
        {
            var from = i / 96f; var to = (i + 1) / 96f;
            var distance = MathF.Abs((from + to) / 2 - head); distance = MathF.Min(distance, 1 - distance);
            var intensity = (settings.ReduceMotion ? .38f : MathF.Exp(-distance * distance / .0065f)) * Math.Clamp(settings.GlowStrength, 0, 1);
            if (intensity < .02f) continue;
            var a = FramePoint(minimum, maximum, from); var b = FramePoint(minimum, maximum, to);
            var color = Accent; color.W = intensity * .14f; draw.AddLine(a, b, Color(color), 7);
            color.W = intensity * .45f; draw.AddLine(a, b, Color(color), 3.2f);
            color = Vector4.Lerp(Accent, Vector4.One, .6f); color.W = intensity; draw.AddLine(a, b, Color(color), 1.2f);
        }
    }
    private static Vector2 FramePoint(Vector2 min, Vector2 max, float t)
    {
        var r = Math.Min(5, Math.Min(max.X - min.X, max.Y - min.Y) / 2);
        var w = max.X - min.X - 2 * r; var h = max.Y - min.Y - 2 * r; var arc = MathF.PI * r / 2;
        var d = (t - MathF.Floor(t)) * (2 * w + 2 * h + 4 * arc);
        Vector2 Curve(Vector2 center, float start, float distance) => center + new Vector2(MathF.Cos(start + distance / r), MathF.Sin(start + distance / r)) * r;
        if (d < w) return new(min.X + r + d, min.Y); d -= w;
        if (d < arc) return Curve(new(max.X - r, min.Y + r), -MathF.PI / 2, d); d -= arc;
        if (d < h) return new(max.X, min.Y + r + d); d -= h;
        if (d < arc) return Curve(new(max.X - r, max.Y - r), 0, d); d -= arc;
        if (d < w) return new(max.X - r - d, max.Y); d -= w;
        if (d < arc) return Curve(new(min.X + r, max.Y - r), MathF.PI / 2, d); d -= arc;
        if (d < h) return new(min.X, max.Y - r - d); d -= h;
        return Curve(new(min.X + r, min.Y + r), MathF.PI, d);
    }
}
