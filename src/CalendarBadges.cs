using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private float CalendarBadgeHeight => ImGui.GetFontSize() * CalendarScale(store.Appearance.CalendarFontScale) + 16;
    private Guid? calendarDetailId;

    private void DrawEventCategoryEditor()
    {
        var type = EventTypes.For(editor);
        if (editor.PersonalOnly) ImGui.TextColored(type.Color, "PE · Personal Event");
        else if (ImGui.BeginCombo("Event type", type.Code + " · " + type.Name))
        {
            foreach (var category in EventTypes.All.Where(c => c.Code != "PE"))
                if (ImGui.Selectable(category.Code + " · " + category.Name, type.Code == category.Code)) editor.EventType = category.Code;
            ImGui.EndCombo();
        }
        ImGui.TextWrapped("Event type controls the calendar color. Visibility controls who can see it; a category never makes an event private or official.");
        Text("Tags (comma-separated)", editor.Tags, 251, value => editor.Tags = value);
        ImGui.TextDisabled("Examples: RP, Combat, Market, Performance, Ball, Hunt, Raid, Investigation, Venue");
    }

    private bool DrawCalendarBadge(EventRecord item, Vector2 top, float width, bool dayHovered, bool clicked, bool continuesLeft, bool continuesRight)
    {
        var badge = EventTypes.For(item);
        var draw = ImGui.GetWindowDrawList();
        var bottom = top + new Vector2(width, CalendarBadgeHeight);
        var hovered = dayHovered && ImGui.IsMouseHoveringRect(top, bottom);
        var corners = ImDrawFlags.RoundCornersNone;
        if (!continuesLeft) corners |= ImDrawFlags.RoundCornersLeft;
        if (!continuesRight) corners |= ImDrawFlags.RoundCornersRight;
        if (!continuesLeft || !continuesRight) corners &= ~ImDrawFlags.RoundCornersNone;
        if (store.Appearance.Glows)
            for (var ring = 3; ring >= 1; ring--)
                draw.AddRect(top - new Vector2(ring), bottom + new Vector2(ring), Color(badge.Color with { W = .08f * Math.Clamp(store.Appearance.GlowStrength, 0, 1) }), 10, corners, 2);
        draw.AddRectFilled(top, bottom, Color(badge.Color with { W = calendarDetailId == item.Id ? .35f : hovered ? .26f : .14f }), 10, corners);
        draw.AddRect(top, bottom, Color(badge.Color), 10, corners, calendarDetailId == item.Id ? 2.5f : 1.5f);
        var fontSize = Math.Min(ImGui.GetFontSize() * CalendarScale(store.Appearance.CalendarFontScale), Math.Max(8, (width - 34) / ImGui.CalcTextSize(badge.Code).X * ImGui.GetFontSize()));
        draw.AddText(ImGui.GetFont(), fontSize, top + new Vector2(8, (CalendarBadgeHeight - fontSize) / 2), Color(new Vector4(.96f,.97f,1,1)), badge.Code);
        var center = new Vector2(bottom.X - 14, top.Y + CalendarBadgeHeight / 2);
        var infoHovered = false;
        if (width > 55)
        {
            draw.AddCircleFilled(center, 8, Color(new Vector4(.02f,.03f,.06f,1)));
            draw.AddCircle(center, 8, Color(badge.Color));
            draw.AddText(center - ImGui.CalcTextSize("i") / 2, Color(new Vector4(1)), "i");
            infoHovered = dayHovered && ImGui.IsMouseHoveringRect(center - new Vector2(10), center + new Vector2(10));
        }
        if (hovered && !infoHovered && clicked) calendarDetailId = item.Id;
        return infoHovered;
    }

    private void CalendarBadgeTooltip(EventRecord item, DateTime day)
    {
        var badge = EventTypes.For(item);
        ImGui.PushStyleColor(ImGuiCol.Border, badge.Color);
        ImGui.BeginTooltip(); ImGui.PushTextWrapPos(ImGui.GetFontSize() * 26);
        ImGui.TextColored(badge.Color, badge.Name);
        ImGui.TextWrapped(item.Title);
        foreach (var interval in EventRules.Intervals(item))
        {
            var start = interval.Start.ToLocalTime(); var end = interval.End.ToLocalTime();
            if (item.ScheduleMode == "Sessions" && (start.Date >= day.AddDays(1) || end.DateTime <= day)) continue;
            ImGui.TextWrapped(start.Date == end.Date
                ? $"{start:MMM d, yyyy · h:mm tt} – {end:h:mm tt}"
                : $"{start:MMM d, yyyy · h:mm tt} – {end:MMM d, yyyy · h:mm tt}");
        }
        ImGui.PopTextWrapPos(); ImGui.EndTooltip(); ImGui.PopStyleColor();
    }
}
