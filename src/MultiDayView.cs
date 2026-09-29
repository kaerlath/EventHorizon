using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace EventHorizon;
public sealed partial class MainWindow
{
    private void DrawScheduleEditor()
    {
        if (ImGui.BeginCombo("Schedule", editor.ScheduleMode switch { "Continuous" => "Continuous multi-day", "Sessions" => "Multiple sessions", _ => "Single session" }))
        {
            foreach (var mode in new[] { "Single", "Continuous", "Sessions" })
                if (ImGui.Selectable(mode switch { "Continuous" => "Continuous multi-day", "Sessions" => "Multiple sessions", _ => "Single session" }, editor.ScheduleMode == mode))
                {
                    editor.ScheduleMode = mode;
                    if (editor.EndLocal.Length == 0 && DateTime.TryParse(editor.StartLocal, out var start)) editor.EndLocal = start.AddMinutes(editor.DurationMinutes).ToString("yyyy-MM-dd HH:mm");
                    if (mode == "Sessions" && editor.Sessions.Count == 0) editor.Sessions.Add(new() { StartLocal = editor.StartLocal, EndLocal = editor.EndLocal });
                }
            ImGui.EndCombo();
        }
        string Pick(string id, string label, string value)
        {
            ImGui.PushID(id); ImGui.TextUnformatted(label);
            var result = DrawDateTimePicker(value); ImGui.PopID(); return result;
        }
        if (editor.ScheduleMode != "Sessions")
        {
            editor.StartLocal = Pick("start", "Starts", editor.StartLocal);
            if (editor.ScheduleMode == "Continuous") editor.EndLocal = Pick("end", "Ends", editor.EndLocal);
        }
        else
        {
            for (var i = 0; i < editor.Sessions.Count; i++)
            {
                var session = editor.Sessions[i]; ImGui.PushID(i);
                session.StartLocal = Pick("start", $"Session {i + 1} starts", session.StartLocal);
                session.EndLocal = Pick("end", "Ends", session.EndLocal);
                if (ImGui.SmallButton("Remove session")) { editor.Sessions.RemoveAt(i); ImGui.PopID(); break; }
                ImGui.Separator(); ImGui.PopID();
            }
            if (editor.Sessions.Count < 12 && ImGui.Button("+ Add date & time", new Vector2(220, 34)))
            {
                var last = editor.Sessions.LastOrDefault();
                var start = DateTime.TryParse(last?.StartLocal ?? editor.StartLocal, out var parsed) ? parsed.AddDays(1) : DateTime.Today.AddDays(1).AddHours(19);
                editor.Sessions.Add(new() { StartLocal = start.ToString("yyyy-MM-dd HH:mm"), EndLocal = start.AddHours(2).ToString("yyyy-MM-dd HH:mm") });
            }
            ImGui.TextWrapped("One event with separate sessions. The in-game calendar and reminders follow each session. Discord and Google receive one overall date range with the full session itinerary; Google marks it as free so gaps do not block your calendar.");
        }
    }

    private void DrawOfficialDetails(EventRecord item)
    {
        ImGui.Spacing(); Heading(item.Title);
        ImGui.TextColored(Accent, "OFFICIAL FFXIV EVENT · INFORMATION ONLY");
        var badge = EventTypes.For(item);
        ImGui.TextColored(badge.Color, badge.Code + " · " + badge.Name);
        DrawSessionSchedule(item);
        ImGui.Spacing(); ImGui.TextWrapped(item.Description);
        ImGui.TextWrapped("Times shown in your local time zone. Dates verified " + OfficialEvents.VerifiedOn + ". Future events appear when confirmed dates are added in an update.");
        if (ImGui.Button("Open Square Enix announcement", new Vector2(290, 36))) OpenUrl(item.SourceUrl);
    }

    private void DrawSessionSchedule(EventRecord item)
    {
        try
        {
            ImGui.TextColored(Muted, "Times shown in " + TimeZoneInfo.Local.DisplayName);
            foreach (var time in EventRules.Intervals(item))
                ImGui.TextWrapped($"{time.Start.ToLocalTime():ddd, MMM d, yyyy h:mm tt} – {time.End.ToLocalTime():ddd, MMM d, yyyy h:mm tt}");
        }
        catch (ArgumentException ex) { ImGui.TextWrapped(ex.Message); }
    }
}
