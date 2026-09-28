using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace EventHorizon;

public sealed partial class MainWindow
{
    private int lastHelpPage = -1;
    private bool helpOverlay;
    private sealed record HelpPage(string Title, string Introduction, string[] Steps, string Remember, string Action);
    private static readonly HelpPage[] HelpPages =
    [
        new("Welcome to Event Horizon",
            "Plan an event, share it with your Discord community, and choose your own reminders. Follow the guide in order, or jump to any topic. You can return using Help & Walkthrough in the sidebar.",
            ["Start with Connect Discord if you will publish events or browse your community's events. You can create local drafts without connecting.",
             "Use + Create Event to prepare your event. Saving a draft does not post anything.",
             "Publish only when the date, time, server and channel are correct.",
             "Choose personal on-screen reminders and/or Google Calendar separately for each event you want to follow."],
            "Three separate choices: publishing shares an event with Discord; personal reminders show a popup in your game; Google Calendar adds a copy to your own calendar. None automatically enables the others.", ""),
        new("Connect Discord and choose a destination",
            "Use your own Discord account. The server also needs the Event Horizon bot. Ordinary players use the shared service; you do not need to create a Discord application or set up a relay.",
            ["Open Connection settings (or Connect Discord). Leave Lock relay address checked to use the shared service.",
             "If the bot is not installed, click Add Event Horizon bot to a server. In Discord, choose the server and approve the requested permissions. You need Manage Server permission; otherwise, use Copy bot installation link and share it with an administrator. Return to the plugin when installation is complete.",
             "Click Connect Discord account, then Open authorization page. Check that the connection reference matches the plugin.",
             "In your browser, choose Continue to Discord and authorize your account. Return to the plugin and click Finish connection.",
             "Leave Stay connected while playing enabled in Connection settings to stay linked for your play session, even with the planner closed. Logout or unloading the plugin disconnects; link again next session. If the game crashes or loses contact, the connection expires after 15 minutes without renewal. Turning the option off restores the one-hour limit. Offline Discord reminders and Google updates continue separately.",
             "Click Load servers. Choose your Server, then Refresh channels if needed and choose an Announcement channel.",
             "Check that the sidebar says Connected and shows your intended community. The announcement channel receives the composed image; the native Discord event is created in the server."],
            "Only eligible servers/channels appear for publishing. Ask an administrator about bot installation and event/channel permissions if your destination is missing. Linking uses your browser; never enter passwords or bot tokens in Event Horizon.", "settings"),
        new("Create and save your first event",
            "Your event begins as a local draft. Cancel opens a separate confirmation: Discard changes leaves the editor; Keep editing returns to your work. You can save it, come back later, and adjust it before sharing.",
            ["Click + Create Event and enter a short, clear Title and Description. Include what guests need to know or bring.",
             "For a private reminder, choose Visibility: Personal — only me. Save personal event puts it in Events without posting to Discord. Save & sync my Google Calendar also queues a private Google copy after you connect your accounts. In View, enable Remind me on screen under Personal Reminder and enable reminder popups. Google calendar sharing still applies.",
             "Optional: in a saved personal event, expand Discord DM reminders. Choose advance and near-start times, then check Send me Discord reminders. Send test reminder to me checks delivery. Uncheck to cancel unsent DMs before archiving. These run while the game is closed; allow bot DMs. Google sync and in-game popups stay separate.",
             "Choose the date using the calendar, then select the hour, minute and AM/PM. Check Time zone and Duration carefully.",
             "Enter World and Location so players can find the event. Organizer can be an in-game or display name.",
             "Check Discord destination. If needed, choose your destination in Connection settings and then click Use destination from Settings in the editor.",
             "Click Save draft. Find it under Drafts and choose Edit when ready to continue. Save as template creates a reusable starting point."],
            "Changing the Organizer display name does not transfer editing access. The Discord account that published the event remains its editor. Save draft and Save local changes do not update Discord.", "create"),
        new("Add an image and style the announcement",
            "The relay turns your event into an announcement image, including your chosen fonts and text effects.",
            ["Under Event banner, click Choose image and select a PNG or JPEG, up to 4 MB. A wide image naturally fills a wide banner; portrait images keep their proportions.",
             "Expand Announcement typography & effects. Select Title, Description default, or an individual paragraph. Separate paragraphs with a blank line.",
             "Choose a Font family and text size. Add bold, italic, underline, outline, glow or color as desired. Use enough contrast to keep the event readable.",
             "For a custom font, expand Import custom font / My font library, choose a permitted font file, confirm its upload/embedding license, then select Upload and use font.",
             "While connected to Discord, click Render announcement preview. Close the preview to continue editing; render again after changing text or styling."],
            "Rendering a preview does not publish anything. Announcement fonts and effects appear in the image; the native Discord event keeps plain text. Imported fonts are tied to your Discord account and relay.", "drafts"),
        new("Publish, view and update",
            "Publishing creates the Discord event and, when an announcement channel is selected, posts its announcement image.",
            ["Review the title, date, AM/PM, time zone, duration, server and announcement channel. Check your rendered preview.",
             "Click Publish to Discord. Wait for the result at the bottom of the window. If it reports a partial failure, follow the message rather than creating a duplicate event.",
             "Open Events and select the event's day on the calendar. Click View in the list below. Other members can use Refresh community events to find eligible published events.",
             "Use Open in Discord to verify the post. Show announcement artwork displays the saved image; turn it off to see native event details. Click the image to enlarge it.",
             "To change a published event, choose Edit event and then Save & Sync to Discord. This updates the existing event and announcement. Use Refresh artwork to retrieve the latest image."],
            "Save local changes does not sync to Discord. Archive locally hides a local entry; it does not cancel the Discord event. Recurrence and signup groups are planning features, not automatic recurring posts or live role attendance.", "events"),
        new("Set your personal on-screen reminders",
            "There are TWO switches: a master switch for popups and a personal choice on each event. Both must be enabled.",
            ["Open Reminders in the sidebar and check Enable on-screen event reminders.",
             "Open Events, select the event's date, and click View. Drafts can also have personal reminders.",
             "In the Event Details panel, find PERSONAL REMINDER and check Remind me on screen. This is the per-event checkbox that requests your notification.",
             "Choose how early to notify you (for example, 15 minutes before). If the panel says popups are off, click Enable reminder popups.",
             "You may close the main Event Horizon window. At the chosen time, a separate small window shows the countdown. Use Open event, Dismiss, or Snooze 5 minutes.",
             "To stop one event's reminder, uncheck Remind me on screen or remove it on the Reminders page. To stop all popups, turn off the master switch."],
            "Publishing, viewing an event, or adding it to Google Calendar does NOT enable this checkbox for you. The game and plugin must be running. Reminders use the saved schedule: refresh community events to receive changes. Popups stop ten minutes after the start or when the event ends, whichever comes first.", "reminders"),
        new("Connect your own Google Calendar",
            "Google Calendar is optional. Each player connects their own account and chooses their own events; you do not need permission to edit the organizer's event.",
            ["Connect Discord first. Open Connection settings and expand Google Calendar (optional).",
             "Click Refresh Google status. If available, choose Connect Google Calendar, then Open Google authorization.",
             "Check the connection reference and sign in to the Google account you want to use. Complete the consent steps in the browser.",
             "Return to Event Horizon and click Refresh Google status. If Prepare calendar appears, click it. Wait for Dedicated Event Horizon calendar ready.",
             "Open an event in View, expand My Google Calendar in the details panel, and click Add to my Google Calendar.",
             "Look for the dedicated Event Horizon calendar in Google Calendar. Configure notifications in Google Calendar if you also want phone/browser alerts. In Connection settings, Refresh my subscriptions lets you check each event's sync status."],
            "This creates your personal calendar copy and follows organizer updates through the relay, even when the game is closed. Google edits do not change Discord. Remove from my calendar removes your copy only. On-screen game reminders still require the separate PERSONAL REMINDER checkbox.", "settings"),
        new("If something doesn't work",
            "Most setup issues can be resolved without recreating your event.",
            ["Discord connection expired: reconnect in Connection settings. Sessions last up to an hour; reloading the plugin requires linking again. Saved drafts and reminder choices remain.",
             "No server/channel: verify the bot is installed and your account and the bot have the required event and channel permissions. Then Load servers / Refresh channels again.",
             "No popup: check both Enable on-screen event reminders AND Remind me on screen for that event. Check the saved time zone, reminder lead time, and whether it was dismissed or snoozed. Normal game/Dalamud UI hiding can also hide windows.",
             "Google unavailable or access denied: the relay operator may need to finish setup or allow your account during testing. Players do not need their own Google Cloud project.",
             "To delete an event you published, open View, choose Delete published event..., and confirm Delete everywhere. The relay removes the Discord event and announcement and queues Google-copy cleanup for subscribers. History offers Check deletion status and Retry remaining cleanup. Disconnected Google accounts must reconnect; independently exported copies cannot be removed. Archive locally is different and keeps remote copies.",
             "Google event missing: confirm the dedicated calendar is ready and you clicked Add to my Google Calendar. Refresh Google status / Refresh my subscriptions; use Retry enabled event syncs if needed.",
             "Image differs from your edits: save and sync the event, then Refresh artwork. Older publications may display a labelled reconstruction. Keep drafts and avoid deleting/recreating a publication to troubleshoot it."],
            "Help & Walkthrough is always available from the sidebar. You can repeat any step without enabling reminders, authorizing accounts, or posting automatically.", "settings"),
    ];

    private void OpenHelp(int page = -1)
    {
        if (page >= 0) { store.HelpStep = page; TrySaveSettings(); }
        if (editing) { helpOverlay = true; return; }
        section = "Help"; selected = null; editing = false;
    }

    private void DrawHelpOverlay()
    {
        if (!helpOverlay) return;
        ImGui.SetNextWindowSize(new Vector2(760, 680), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Event Horizon help###EventHorizonHelp", ref helpOverlay)) DrawHelp(true);
        ImGui.End();
    }

    private void DrawHelp(bool overlay = false)
    {
        store.HelpStep = Math.Clamp(store.HelpStep, 0, HelpPages.Length - 1);
        var page = HelpPages[store.HelpStep];
        Heading("Help & Walkthrough");
        ImGui.TextColored(Muted, $"Step {store.HelpStep + 1} of {HelpPages.Length} · Your place is saved on this computer");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##helpTopic", page.Title))
        {
            for (var i = 0; i < HelpPages.Length; i++)
                if (ImGui.Selectable($"{i + 1}. {HelpPages[i].Title}", i == store.HelpStep)) { store.HelpStep = i; TrySaveSettings(); }
            ImGui.EndCombo();
        }
        ImGui.BeginChild("HelpSteps", new Vector2(0, -110), false);
        if (lastHelpPage != store.HelpStep) { ImGui.SetScrollY(0); lastHelpPage = store.HelpStep; }
        Heading(page.Title); ImGui.TextWrapped(page.Introduction); ImGui.Spacing();
        for (var i = 0; i < page.Steps.Length; i++)
        {
            ImGui.TextColored(Accent, $"{i + 1:00}"); ImGui.SameLine();
            ImGui.BeginGroup(); ImGui.PushTextWrapPos(0); ImGui.TextUnformatted(page.Steps[i]); ImGui.PopTextWrapPos(); ImGui.EndGroup();
            ImGui.Spacing();
        }
        ImGui.Separator(); ImGui.TextColored(Accent, "Remember"); ImGui.TextWrapped(page.Remember);
        if (page.Action.Length > 0)
        {
            ImGui.Spacing();
            var label = page.Action switch { "create" => "Create an event", "drafts" => "Open drafts", "events" => "Open events", "reminders" => "Open reminder settings", _ => "Open connection settings" };
            ImGui.BeginDisabled(editing);
            if (ImGui.Button(label, new Vector2(Math.Max(280, ImGui.CalcTextSize(label).X + 30), 40)))
            {
                if (page.Action == "settings") OpenSettings();
                else if (page.Action == "create") BeginEdit(NewRecord());
                else { section = page.Action switch { "drafts" => "Drafts", "reminders" => "Reminders", _ => "Events" }; selected = null; }
            }
            ImGui.EndDisabled();
            if (editing) ImGui.TextWrapped("Your editor remains open. Save your draft before using a shortcut to another screen.");
            ImGui.TextWrapped("Return with Help & Walkthrough in the sidebar. Your place will be kept.");
        }
        ImGui.EndChild();
        ImGui.BeginDisabled(store.HelpStep == 0);
        if (ImGui.Button("Back", new Vector2(100, 36))) { store.HelpStep--; TrySaveSettings(); }
        ImGui.EndDisabled(); ImGui.SameLine();
        if (ImGui.Button(store.HelpStep == HelpPages.Length - 1 ? "Finish walkthrough" : "Next step", new Vector2(210, 36)))
        {
            if (store.HelpStep < HelpPages.Length - 1) store.HelpStep++;
            else { store.ShowWelcomeHelp = false; if (overlay) helpOverlay = false; else section = "Events"; }
            TrySaveSettings();
        }
        ImGui.SameLine();
        if (ImGui.Button("Skip for now", new Vector2(145, 36))) { store.ShowWelcomeHelp = false; if (overlay) helpOverlay = false; else section = "Events"; TrySaveSettings(); }
        var welcome = store.ShowWelcomeHelp;
        if (ImGui.Checkbox("Start with this guide after reloading the plugin", ref welcome)) { store.ShowWelcomeHelp = welcome; TrySaveSettings(); }
    }
}
