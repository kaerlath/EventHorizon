using System.Numerics;
using System.Globalization;
using Dalamud.Bindings.ImGui;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private static float CalendarScale(float scale) => float.IsFinite(scale) ? Math.Clamp(scale, .7f, 1.5f) : 1;
    private static string CalendarStyleKey(EventRecord item) => item.DiscordEventId.Length > 0
        ? $"{item.RelayOrigin}|{item.GuildId}|{item.DiscordEventId}" : item.Id.ToString();
    private CalendarTextStyle CalendarStyle(EventRecord item)
    {
        var settings = store.Appearance;
        settings.CalendarEventStyles ??= new();
        var style = settings.CalendarEventStyles.GetValueOrDefault(CalendarStyleKey(item));
        return new() { Scale = CalendarScale(style?.Scale ?? settings.CalendarFontScale), Color = style?.Color ?? settings.CalendarTextColor };
    }
    private static string[] CalendarLines(string text, float width, float scale) => CalendarText.Wrap(text, width,
        s => ImGui.CalcTextSize(s).X * scale);
    private void DrawCalendarTextStyle(EventRecord item)
    {
        if (!ImGui.CollapsingHeader("Personal calendar text")) return;
        var settings = store.Appearance;
        settings.CalendarEventStyles ??= new();
        var key = CalendarStyleKey(item);
        var custom = settings.CalendarEventStyles.ContainsKey(key);
        var style = CalendarStyle(item);
        if (ImGui.Checkbox("Customize this event on my calendar", ref custom))
        {
            if (custom) settings.CalendarEventStyles[key] = style;
            else settings.CalendarEventStyles.Remove(key);
            TrySaveSettings();
        }
        if (!custom) return;
        var changed = false;
        var scale = style.Scale;
        if (ImGui.SliderFloat("Event text size", ref scale, .7f, 1.5f, "%.2fx")) { style.Scale = scale; changed = true; }
        changed |= EditColor("Event text color", style.Color, x => style.Color = x);
        ImGui.TextWrapped("Only your calendar changes. Other players and the Discord announcement keep their own appearance.");
        if (changed) { settings.CalendarEventStyles[key] = style; TrySaveSettings(); }
    }
}
