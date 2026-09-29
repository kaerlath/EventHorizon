using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private IFontHandle? titleFont, headingFont, bodyFont;
    private IFontHandle? navIconFont;
    private DateTime calendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private bool animateOrb = true;
    private string titleFontName = "Default";

    private void InitializeFonts(IUiBuilder ui, string directory)
    {
        navIconFont = ui.IconFontHandle;
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        // Read installed fonts in place. Do not redistribute proprietary Windows font files.
        var candidates = new[] { Path.Combine(directory, "fonts", "Cinzel.ttf"), Path.Combine(fonts, "FELIXTI.TTF"), Path.Combine(fonts, "constan.ttf"), Path.Combine(fonts, "times.ttf") };
        var path = candidates.FirstOrDefault(File.Exists);
        titleFontName = path is null ? "Dalamud default" : Path.GetFileNameWithoutExtension(path) switch { "FELIXTI" => "Felix Titling", "constan" => "Constantia", "times" => "Times New Roman", _ => "Cinzel" };
        titleFont = ui.FontAtlas.NewDelegateFontHandle(step => step.OnPreBuild(toolkit =>
        {
            if (path is null) toolkit.AddDalamudDefaultFont(30);
            else toolkit.AddFontFromFile(path, new SafeFontConfig { SizePx = 30 });
        }));
        headingFont = ui.FontAtlas.NewDelegateFontHandle(step => step.OnPreBuild(toolkit => toolkit.AddDalamudDefaultFont(30)));
        bodyFont = ui.FontAtlas.NewDelegateFontHandle(step => step.OnPreBuild(toolkit => toolkit.AddDalamudDefaultFont(18)));
    }

    private bool NavigationItem(string name, int count)
    {
        var start = ImGui.GetCursorScreenPos();
        var clicked = ImGui.Selectable("##nav" + name, section == name && !editing, ImGuiSelectableFlags.None, new Vector2(0, 38));
        var end = ImGui.GetCursorScreenPos();
        if (section == name && !editing)
        {
            ImGui.GetWindowDrawList().AddRectFilled(start, start + new Vector2(3, 38), Color(Accent), 2);
            DrawFrameLight(start + new Vector2(3), start + new Vector2(ImGui.GetContentRegionAvail().X - 3, 35));
        }
        ImGui.SetCursorScreenPos(start + new Vector2(8, 9));
        var icon = name switch { "Events" => FontAwesomeIcon.CalendarAlt, "Drafts" => FontAwesomeIcon.FileAlt,
            "Recurring" => FontAwesomeIcon.SyncAlt, "Templates" => FontAwesomeIcon.Book, _ => FontAwesomeIcon.History };
        using (navIconFont?.Push()) ImGui.TextColored(Accent, char.ConvertFromUtf32((int)icon));
        ImGui.SetCursorScreenPos(start + new Vector2(40, 9)); ImGui.TextUnformatted(name);
        if (count > 0)
        { ImGui.SetCursorScreenPos(start + new Vector2(163, 9)); ImGui.TextColored(Accent, count.ToString()); }
        ImGui.SetCursorScreenPos(end);
        return clicked;
    }

    private void Heading(string text)
    {
        using var font = headingFont?.Push();
        ImGui.TextWrapped(text);
    }

    private void DrawHeader()
    {
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilledMultiColor(start, start + new Vector2(width, 84), Color(BackgroundSurface(.20f)), Color(BackgroundSurface(.045f)), Color(BackgroundSurface(.02f)), Color(BackgroundSurface(.12f)));
        draw.AddLine(start + new Vector2(0, 83), start + new Vector2(width, 83), Color(Surface(.45f)));
        for (var i = 0; i < 22; i++)
            draw.AddCircleFilled(start + new Vector2((i * 173.1f) % width, 8 + (i * 23.7f) % 67), .6f, 0x448BAEDA);
        ImGui.SetCursorScreenPos(start + new Vector2(8, 0));
        DrawOrb(78);
        ImGui.SetCursorScreenPos(start + new Vector2(8, 0));
        if (ImGui.InvisibleButton("Minimize Event Horizon", new Vector2(78))) MinimizeToIcon();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Click to minimize to the animated icon");
        ImGui.SetCursorScreenPos(start + new Vector2(96, 25));
        using (titleFont?.Push()) ImGui.TextColored(new Vector4(.87f, .91f, 1, 1), "E V E N T   H O R I Z O N");
        if (width > 900)
        {
            ImGui.SetCursorScreenPos(start + new Vector2(width - 230, 31));
            ConnectionBadge();
        }
        ImGui.SetCursorScreenPos(start + new Vector2(0, 87));
        ImGui.Separator();
    }

    private void DrawOrb(float size)
    {
        var top = ImGui.GetCursorScreenPos(); var center = top + new Vector2(size / 2);
        var draw = ImGui.GetWindowDrawList();
        float t = animateOrb && !store.Appearance.ReduceMotion ? (float)(ImGui.GetTime() * Math.Clamp(store.Appearance.AnimationSpeed, .25f, 2) % 3600) : 0;
        for (var i = 5; i > 0; i--)
            draw.AddCircleFilled(center, size * (.35f + i * .026f), ImGui.ColorConvertFloat4ToU32(new Vector4(.19f, .35f, .85f, .015f * (6 - i))), 48);
        var segments = new List<(Vector3 A, Vector3 B, bool Light, bool Spike)>(240);
        for (var ring = 0; ring < 3; ring++)
        {
            var rotation = Quaternion.CreateFromYawPitchRoll(t * (.19f + ring * .045f) * (ring == 1 ? -1 : 1) + ring, .55f + ring * .85f + t * .12f, ring * .8f);
            var radius = size * (.33f + ring * .04f);
            for (var s = 0; s < 72; s++)
            {
                var a = s * MathF.Tau / 72; var b = (s + 1) * MathF.Tau / 72;
                var p = Vector3.Transform(new Vector3(MathF.Cos(a) * radius, MathF.Sin(a) * radius, 0), rotation);
                var q = Vector3.Transform(new Vector3(MathF.Cos(b) * radius, MathF.Sin(b) * radius, 0), rotation);
                segments.Add((p, q, s % 6 == 0, ring == 2 && s % 9 == 0));
            }
        }
        void DrawSegment((Vector3 A, Vector3 B, bool Light, bool Spike) segment)
        {
            var brightness = .35f + .45f * (segment.A.Z / size + .5f);
            var a = center + new Vector2(segment.A.X, segment.A.Y); var b = center + new Vector2(segment.B.X, segment.B.Y);
            draw.AddLine(a, b, ImGui.ColorConvertFloat4ToU32(new Vector4(.38f * brightness, .51f * brightness, .8f * brightness, 1)), Math.Max(2, size * .043f));
            draw.AddLine(a, b, ImGui.ColorConvertFloat4ToU32(new Vector4(.5f, .64f, .88f, brightness)), 1);
            if (segment.Spike)
            {
                var tip = center + new Vector2(segment.A.X, segment.A.Y) * 1.16f;
                draw.AddTriangleFilled(a, b, tip, 0xFFA08570);
            }
            if (segment.Light)
            {
                draw.AddCircleFilled(a, Math.Max(2, size * .031f), 0x555F75FF, 10);
                draw.AddCircleFilled(a, Math.Max(1, size * .017f), segment.A.Z > 0 ? 0xFFF7F0DD : 0xFF867366, 8);
            }
        }
        foreach (var segment in segments.Where(x => (x.A.Z + x.B.Z) < 0).OrderBy(x => x.A.Z)) DrawSegment(segment);
        draw.AddCircleFilled(center, size * .235f, 0xFF18120D, 40);
        for (var latitude = -3; latitude <= 3; latitude++)
        {
            for (var longitude = 0; longitude < 16; longitude++)
            {
                var phi = latitude * .36f; var theta = longitude * MathF.Tau / 16 + t * .14f;
                var z = MathF.Cos(phi) * MathF.Cos(theta);
                if (z < 0) continue;
                var p = center + new Vector2(MathF.Cos(phi) * MathF.Sin(theta), MathF.Sin(phi)) * size * .225f;
                draw.AddCircle(p, size * .019f * (.5f + z), 0xFF746050, 6, 1);
            }
        }
        foreach (var segment in segments.Where(x => (x.A.Z + x.B.Z) >= 0).OrderBy(x => x.A.Z)) DrawSegment(segment);
        ImGui.Dummy(new Vector2(size));
    }

    private string DrawDateTimePicker(string value)
    {
        if (!DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) date = DateTime.Now.AddDays(1);
        ImGui.TextColored(Muted, "Date & time");
        if (ImGui.Button(date.ToString("dddd, MMMM d, yyyy") + "  v", new Vector2(310, 32)))
        { calendarMonth = new DateTime(date.Year, date.Month, 1); ImGui.OpenPopup("Event calendar"); }
        if (ImGui.BeginPopup("Event calendar"))
        {
            if (ImGui.Button("<")) calendarMonth = calendarMonth.AddMonths(-1);
            ImGui.SameLine(); ImGui.SetNextItemWidth(125);
            if (ImGui.BeginCombo("##month", calendarMonth.ToString("MMMM")))
            {
                for (var m = 1; m <= 12; m++) if (ImGui.Selectable(CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m), m == calendarMonth.Month)) calendarMonth = new DateTime(calendarMonth.Year, m, 1);
                ImGui.EndCombo();
            }
            ImGui.SameLine(); ImGui.SetNextItemWidth(85);
            if (ImGui.BeginCombo("##year", calendarMonth.Year.ToString()))
            {
                for (var y = DateTime.Today.Year - 1; y <= DateTime.Today.Year + 10; y++) if (ImGui.Selectable(y.ToString(), y == calendarMonth.Year)) calendarMonth = new DateTime(y, calendarMonth.Month, 1);
                ImGui.EndCombo();
            }
            ImGui.SameLine(); if (ImGui.Button(">")) calendarMonth = calendarMonth.AddMonths(1);
            if (ImGui.BeginTable("days", 7, ImGuiTableFlags.SizingFixedSame))
            {
                foreach (var day in new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" }) { ImGui.TableNextColumn(); ImGui.TextUnformatted(day); }
                ImGui.TableNextRow();
                for (var blank = 0; blank < (int)calendarMonth.DayOfWeek; blank++) { ImGui.TableNextColumn(); ImGui.Dummy(new Vector2(30, 28)); }
                for (var day = 1; day <= DateTime.DaysInMonth(calendarMonth.Year, calendarMonth.Month); day++)
                {
                    ImGui.TableNextColumn();
                    if (ImGui.Selectable(day.ToString(), date.Date == calendarMonth.AddDays(day - 1), ImGuiSelectableFlags.None, new Vector2(30, 28)))
                    { date = calendarMonth.AddDays(day - 1).Add(date.TimeOfDay); ImGui.CloseCurrentPopup(); }
                }
                ImGui.EndTable();
            }
            if (ImGui.Button("Today")) { date = DateTime.Today.Add(date.TimeOfDay); ImGui.CloseCurrentPopup(); }
            ImGui.SameLine(); if (ImGui.Button("Tomorrow")) { date = DateTime.Today.AddDays(1).Add(date.TimeOfDay); ImGui.CloseCurrentPopup(); }
            ImGui.EndPopup();
        }
        ImGui.SameLine(); ImGui.SetNextItemWidth(70);
        if (ImGui.BeginCombo("##hour", date.ToString("hh")))
        { for (var h = 1; h <= 12; h++) if (ImGui.Selectable(h.ToString("00"), (date.Hour % 12 == h % 12))) date = date.Date.AddHours(h % 12 + (date.Hour >= 12 ? 12 : 0)).AddMinutes(date.Minute); ImGui.EndCombo(); }
        ImGui.SameLine(); ImGui.SetNextItemWidth(70);
        if (ImGui.BeginCombo("##minute", date.ToString("mm")))
        { for (var m = 0; m < 60; m++) if (ImGui.Selectable(m.ToString("00"), m == date.Minute)) date = date.Date.AddHours(date.Hour).AddMinutes(m); ImGui.EndCombo(); }
        ImGui.SameLine(); ImGui.SetNextItemWidth(75);
        if (ImGui.BeginCombo("##ampm", date.Hour >= 12 ? "PM" : "AM"))
        { foreach (var period in new[] { 0, 12 }) if (ImGui.Selectable(period == 0 ? "AM" : "PM", date.Hour / 12 == period / 12)) date = date.Date.AddHours(date.Hour % 12 + period).AddMinutes(date.Minute); ImGui.EndCombo(); }
        return date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private void DrawBannerSelector()
    {
        ImGui.TextColored(Muted, "Event banner");
        var pickerLabel = editor.BannerPath.Length == 0 ? "Choose image" : "Change image";
        if (ImGui.Button(pickerLabel, new Vector2(Math.Max(230, ImGui.CalcTextSize(pickerLabel).X + 64), 40))) Run(async () =>
        {
            var path = await BannerPicker.Choose();
            if (path is null) return () => { };
            if (new FileInfo(path).Length > BannerUpload.MaxBytes) throw new ArgumentException("Choose an image smaller than 4 MB.");
            _ = BannerUpload.Encode(await File.ReadAllBytesAsync(path));
            return () => { editor.BannerPath = path; editor.RemoveBanner = false; message = "Banner selected. It will upload when you publish or sync."; };
        });
        ImGui.SameLine();
        if (ImGui.Button("Remove banner", new Vector2(150, 32))) { editor.BannerPath = ""; editor.RemoveBanner = true; }
        if (editor.BannerPath.Length > 0) ImGui.TextWrapped(Path.GetFileName(editor.BannerPath));
        ImGui.TextDisabled(editor.PersonalOnly ? "PNG or JPEG, up to 4 MB. Personal banners stay on this computer." : "PNG or JPEG, up to 4 MB. Sent with your Discord event and announcement.");
    }
}
