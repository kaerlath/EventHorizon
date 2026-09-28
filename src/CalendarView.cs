using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private DateTime calendarFocus = DateTime.Today, calendarSelection = DateTime.Today;
    private bool calendarWeekly;

    private EventRecord[] DrawEventCalendar()
    {
        var zone = TimeZoneInfo.Local;
        // Keep past published events available when browsing earlier months.
        var events = EventLibrary().Where(e => e.Status is not ("Archived" or "Deleting" or "Deleted") && e.Status != "Draft" &&
            (string.IsNullOrWhiteSpace(search) || (e.Title + " " + e.World + " " + e.Location).Contains(search, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (ImGui.Button("<##previous", new Vector2(34, 30)))
            calendarSelection = calendarFocus = calendarWeekly ? calendarFocus.AddDays(-7) : calendarFocus.AddMonths(-1);
        ImGui.SameLine();
        if (ImGui.Button("Today", new Vector2(70, 30))) calendarFocus = calendarSelection = DateTime.Today;
        ImGui.SameLine();
        if (ImGui.Button(">##next", new Vector2(34, 30)))
            calendarSelection = calendarFocus = calendarWeekly ? calendarFocus.AddDays(7) : calendarFocus.AddMonths(1);
        ImGui.SameLine();
        var first = calendarWeekly ? EventCalendar.WeekStart(calendarFocus) : EventCalendar.MonthStart(calendarFocus);
        ImGui.TextColored(Accent, calendarWeekly ? $"{first:MMM d} – {first.AddDays(6):MMM d, yyyy}" : calendarFocus.ToString("MMMM yyyy"));
        ImGui.SameLine(); ImGui.SetNextItemWidth(105);
        if (ImGui.BeginCombo("##calendarView", calendarWeekly ? "Week" : "Month"))
        {
            if (ImGui.Selectable("Month", !calendarWeekly)) { calendarWeekly = false; calendarFocus = calendarSelection; }
            if (ImGui.Selectable("Week", calendarWeekly)) { calendarWeekly = true; calendarFocus = calendarSelection; }
            ImGui.EndCombo();
        }
        first = calendarWeekly ? EventCalendar.WeekStart(calendarFocus) : EventCalendar.MonthStart(calendarFocus);
        ImGui.TextColored(Muted, "Local and loaded community events · Times shown in " + zone.DisplayName);
        var intervals = events.Select(item => (Item: item, Times: EventCalendar.Interval(item, zone)))
            .Where(x => x.Times is not null).OrderBy(x => x.Times!.Value.Start).ToArray();
        EventRecord[] OnDay(DateTime day) => intervals.Where(x => x.Times!.Value.Start < day.AddDays(1) && x.Times.Value.End > day)
            .Select(x => x.Item).ToArray();
        var rows = calendarWeekly ? 1 : 6;
        var height = calendarWeekly ? 200f : 76f;
        if (ImGui.BeginTable("EventCalendar", 7, ImGuiTableFlags.SizingStretchSame))
        {
            foreach (var name in new[] { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" })
            { ImGui.TableNextColumn(); ImGui.TextColored(Muted, name); }
            for (var index = 0; index < rows * 7; index++)
            {
                ImGui.TableNextColumn();
                var day = first.AddDays(index); var entries = OnDay(day);
                var start = ImGui.GetCursorScreenPos(); var size = new Vector2(ImGui.GetContentRegionAvail().X, height);
                var active = day == calendarSelection; var today = day == DateTime.Today;
                if (ImGui.InvisibleButton("day" + index, size)) calendarSelection = day;
                var hover = ImGui.IsItemHovered(); var draw = ImGui.GetWindowDrawList();
                draw.AddRectFilled(start, start + size, Color(Surface(active ? .30f : hover ? .18f : .065f)), 5);
                draw.AddRect(start, start + size, Color(today || active ? Accent : Surface(.25f)), 5);
                if (active) DrawFrameLight(start + new Vector2(3), start + size - new Vector2(3));
                draw.PushClipRect(start + new Vector2(4), start + size - new Vector2(4), true);
                draw.AddText(start + new Vector2(8, 6), day.Month == calendarFocus.Month || calendarWeekly ? 0xFFFFECDE : 0xFF978373, day.Day.ToString());
                if (entries.Length > 0)
                {
                    draw.AddCircleFilled(start + new Vector2(size.X - 12, 13), 3, Color(Accent));
                    var max = calendarWeekly ? 7 : 1;
                    for (var j = 0; j < Math.Min(entries.Length, max); j++)
                        draw.AddText(start + new Vector2(8, 29 + j * 20), 0xFFE8C6B9, entries[j].Title);
                    if (entries.Length > max) draw.AddText(start + new Vector2(8, height - 22), 0xFFD5AE9C, $"+{entries.Length - max} more");
                }
                draw.PopClipRect();
                if (hover && entries.Length > 0)
                {
                    ImGui.BeginTooltip(); ImGui.TextUnformatted(day.ToString("dddd, MMMM d"));
                    foreach (var item in entries.Take(12))
                    { var time = EventCalendar.Interval(item, zone)!.Value.Start; ImGui.TextUnformatted($"{time:MMM d, h:mm tt} · {item.Title}"); }
                    if (entries.Length > 12) ImGui.TextUnformatted($"+{entries.Length - 12} more");
                    ImGui.EndTooltip();
                }
            }
            ImGui.EndTable();
        }
        ImGui.Spacing(); Heading(calendarSelection.ToString("dddd, MMMM d, yyyy"));
        var result = OnDay(calendarSelection);
        ImGui.TextColored(Muted, result.Length == 1 ? "1 event" : $"{result.Length} events");
        if (result.Length == 0) ImGui.TextWrapped("No published events on this day. Select another date or create a gathering.");
        if (ImGui.Button("+ Create event on this day", new Vector2(240, 34)))
        { var item = NewRecord(); item.StartLocal = calendarSelection.AddHours(19).ToString("yyyy-MM-dd HH:mm"); BeginEdit(item); }
        ImGui.Spacing();
        return result;
    }
}
