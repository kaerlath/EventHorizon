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
        var showOfficial = store.Appearance.ShowOfficialEvents;
        if (ImGui.Checkbox("Show official FFXIV events", ref showOfficial)) { store.Appearance.ShowOfficialEvents = showOfficial; TrySaveSettings(); }
        var hiddenTypes = store.Appearance.HiddenEventTypes ??= [];
        var enabledTypes = EventTypes.All.Count(t => !hiddenTypes.Contains(t.Code));
        if (ImGui.CollapsingHeader($"Event type legend & filters ({enabledTypes}/{EventTypes.All.Length} on)###EventTypeFilters"))
        {
            var changed = false;
            if (ImGui.Button("All on")) { hiddenTypes.Clear(); changed = true; }
            ImGui.SameLine();
            if (ImGui.Button("All off")) { hiddenTypes.UnionWith(EventTypes.All.Select(t => t.Code)); changed = true; }
            foreach (var type in EventTypes.All)
            {
                var enabled = !hiddenTypes.Contains(type.Code);
                ImGui.PushStyleColor(ImGuiCol.Text, type.Color);
                if (ImGui.Checkbox(type.Code + " · " + type.Name, ref enabled))
                {
                    if (enabled) hiddenTypes.Remove(type.Code); else hiddenTypes.Add(type.Code);
                    changed = true;
                }
                ImGui.PopStyleColor();
            }
            if (changed) TrySaveSettings();
            ImGui.TextWrapped("These filters save on this computer and affect only your calendar and its event list. Reminders and Discord/Google copies are unchanged. All on enables every type; Show official FFXIV events and search still apply.");
            ImGui.TextWrapped("Color identifies the category, not the publisher. Official Square Enix entries are read-only. Hover only the i for type, title and local time. Click the capsule body for full details below.");
        }
        ImGui.TextDisabled("Official dates verified " + OfficialEvents.VerifiedOn);
        var events = EventLibrary().Concat(showOfficial ? OfficialEvents.Items : []).Where(e => store.Appearance.IsEventTypeVisible(e) && e.Status is not ("Archived" or "Deleting" or "Deleted" or "Draft") &&
            (string.IsNullOrWhiteSpace(search) || (e.Title + " " + e.World + " " + e.Location + " " + e.Tags + " " + EventTypes.For(e).Name).Contains(search, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (ImGui.Button("<##previous", new Vector2(34,30))) { calendarSelection = calendarFocus = calendarWeekly ? calendarFocus.AddDays(-7) : calendarFocus.AddMonths(-1); calendarDetailId = null; }
        ImGui.SameLine();
        if (ImGui.Button("Today", new Vector2(70,30))) { calendarFocus = calendarSelection = DateTime.Today; calendarDetailId = null; }
        ImGui.SameLine();
        if (ImGui.Button(">##next", new Vector2(34,30))) { calendarSelection = calendarFocus = calendarWeekly ? calendarFocus.AddDays(7) : calendarFocus.AddMonths(1); calendarDetailId = null; }
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
        ImGui.TextWrapped("Times shown in " + zone.DisplayName + ". Select a day for all events, or a capsule for its details.");
        var valid = events.Where(e => EventCalendar.Interval(e,zone) is not null).OrderBy(e => e.InformationOnly).ThenBy(EventRules.Start).ThenBy(e => e.Id).ToArray();
        EventRecord[] OnDay(DateTime day) => valid.Where(e => EventCalendar.OccursOnDay(e,day,zone)).ToArray();
        var rows = calendarWeekly ? 1 : 6;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(0,3));
        if (ImGui.BeginTable("EventCalendar",7,ImGuiTableFlags.SizingStretchSame))
        {
            foreach (var name in new[] { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" }) { ImGui.TableNextColumn(); ImGui.TextColored(Muted,name); }
            for (var row = 0; row < rows; row++)
            {
                var week = first.AddDays(row * 7);
                var lanes = CalendarLanes.Build(valid,week,zone);
                var shownLanes = Math.Min(lanes.Length, calendarWeekly ? 10 : 6);
                var height = 36 + shownLanes * (CalendarBadgeHeight + CalendarBadgeGap) + 28;
                for (var column = 0; column < 7; column++)
                {
                    ImGui.TableNextColumn();
                    var day = week.AddDays(column); var entries = OnDay(day);
                    var top = ImGui.GetCursorScreenPos(); var size = new Vector2(ImGui.GetContentRegionAvail().X,height);
                    var previousDay = calendarSelection; var previousDetail = calendarDetailId;
                    var clicked = ImGui.InvisibleButton($"day{row}-{column}",size);
                    if (clicked) { calendarSelection = day; calendarDetailId = null; }
                    var hover = ImGui.IsItemHovered(); var draw = ImGui.GetWindowDrawList();
                    draw.AddRectFilled(top,top+size,Color(Surface(day == calendarSelection ? .30f : hover ? .18f : .065f)),5);
                    draw.AddRect(top,top+size,Color(day == calendarSelection || day == DateTime.Today ? Accent : Surface(.25f)),5);
                    if (day == calendarSelection) DrawFrameLight(top+new Vector2(3),top+size-new Vector2(3));
                    draw.PushClipRect(top,top+size,true);
                    draw.AddText(top+new Vector2(8,6),Color(Muted),calendarWeekly ? day.ToString("MMM d") : day.Day.ToString());
                    EventRecord? info = null; var displayed = 0;
                    for (var lane = 0; lane < shownLanes; lane++)
                    {
                        var entry = lanes[lane].FirstOrDefault(e => EventCalendar.OccursOnDay(e,day,zone));
                        if (entry is null) continue;
                        displayed++;
                        var left = column > 0 && CalendarLanes.CrossesMidnight(entry,day,zone);
                        var right = column < 6 && CalendarLanes.CrossesMidnight(entry,day.AddDays(1),zone);
                        var x = left ? 0 : 6; var width = Math.Max(1,size.X-x-(right ? 0 : 6));
                        if (DrawCalendarBadge(entry,top+new Vector2(x,30+lane*(CalendarBadgeHeight+CalendarBadgeGap)),width,hover,clicked,left,right)) info = entry;
                    }
                    if (entries.Length > displayed) draw.AddText(top+new Vector2(8,height-23),Color(Muted),$"+{entries.Length-displayed} more");
                    draw.PopClipRect();
                    if (info is not null)
                    {
                        if (clicked) { calendarSelection = previousDay; calendarDetailId = previousDetail; }
                        CalendarBadgeTooltip(info,day);
                    }
                }
            }
            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
        ImGui.Spacing(); Heading(calendarSelection.ToString("dddd, MMMM d, yyyy"));
        var result = OnDay(calendarSelection);
        ImGui.TextColored(Muted,result.Length == 1 ? "1 event" : $"{result.Length} events");
        if (result.Length == 0) ImGui.TextWrapped("No events match this day and your current filters. Check Event type legend & filters, search, or Show official FFXIV events.");
        if (ImGui.Button("+ Create event on this day",new Vector2(240,34))) { var item=NewRecord(); item.StartLocal=calendarSelection.AddHours(19).ToString("yyyy-MM-dd HH:mm"); BeginEdit(item); }
        ImGui.Spacing(); return result;
    }
}
