# Event Horizon — Player Guide

Version 0.15.0. Open the addon with `/eventhorizon`. This guide is also available in-game under **Help & Walkthrough**.

## 1. Welcome to Event Horizon

Plan an event, share it with your Discord community, and choose your own reminders. Follow the guide in order, or jump to any topic. You can return using Help & Walkthrough in the sidebar.

1. Start with Connect Discord if you will publish events or browse your community's events. You can create local drafts without connecting.
2. Use + Create Event to prepare your event. Saving a draft does not post anything.
3. Publish only when the date, time, server and channel are correct.
4. Choose personal on-screen reminders and/or Google Calendar separately for each event you want to follow.

**Remember:** Three separate choices: publishing shares an event with Discord; personal reminders show a popup in your game; Google Calendar adds a copy to your own calendar. None automatically enables the others.

## 2. Connect Discord and choose a destination

Use your own Discord account. The server also needs the Event Horizon bot. Ordinary players use the shared service; you do not need to create a Discord application or set up a relay.

1. Open Connection settings (or Connect Discord). Leave Lock relay address checked to use the shared service.
2. If the bot is not installed, click Add Event Horizon bot to a server. In Discord, choose the server and approve the requested permissions. You need Manage Server permission; otherwise, use Copy bot installation link and share it with an administrator. Return to the plugin when installation is complete.
3. Click Connect Discord account, then Open authorization page. Check that the connection reference matches the plugin.
4. In your browser, choose Continue to Discord and authorize your account. Return to the plugin and click Finish connection.
5. Click Load servers. Choose your Server, then Refresh channels if needed and choose an Announcement channel.
6. Check that the sidebar says Connected and shows your intended community. The announcement channel receives the composed image; the native Discord event is created in the server.

**Stay connected while playing** is enabled by default in Connection settings. It renews your connection while logged into FFXIV, including when Event Horizon is closed or the game's UI is hidden. Log out or unload the plugin to disconnect; connect again next play session. If the game crashes or loses contact, the relay expires your session after 15 minutes without a check-in. Turning the option off restores a connection of up to one hour. A revoked Discord authorization still requires reconnecting. Offline Discord reminders and Google Calendar updates continue separately.

**Remember:** Only eligible servers/channels appear for publishing. Ask an administrator about bot installation and event/channel permissions if your destination is missing. Linking uses your browser; never enter passwords or bot tokens in Event Horizon.

## 3. Create and save your first event

Your event begins as a local draft. You can save it, come back later, and adjust it before sharing.

1. Click + Create Event and enter a short, clear Title and Description. Include what guests need to know or bring.
2. Choose the date using the calendar, then select the hour, minute and AM/PM. Check Time zone and Duration carefully.
3. Enter World and Location so players can find the event. Organizer can be an in-game or display name.
4. Check Discord destination. If needed, choose your destination in Connection settings and then click Use destination from Settings in the editor.
5. Click Save draft. Find it under Drafts and choose Edit when ready to continue. Save as template creates a reusable starting point.

**Remember:** Changing the Organizer display name does not transfer editing access. The Discord account that published the event remains its editor. Save draft and Save local changes do not update Discord.

## 4. Add an image and style the announcement

The relay turns your event into an announcement image, including your chosen fonts and text effects.

1. Under Event banner, click Choose image and select a PNG or JPEG, up to 4 MB. A wide image naturally fills a wide banner; portrait images keep their proportions.
2. Expand Announcement typography & effects. Select Title, Description default, or an individual paragraph. Separate paragraphs with a blank line.
3. Choose a Font family and text size. Add bold, italic, underline, outline, glow or color as desired. Use enough contrast to keep the event readable.
4. For a custom font, expand Import custom font / My font library, choose a permitted font file, confirm its upload/embedding license, then select Upload and use font.
5. While connected to Discord, click Render announcement preview. Close the preview to continue editing; render again after changing text or styling.

**Remember:** Rendering a preview does not publish anything. Announcement fonts and effects appear in the image; the native Discord event keeps plain text. Imported fonts are tied to your Discord account and relay.

## 5. Publish, view and update

Publishing creates the Discord event and, when an announcement channel is selected, posts its announcement image.

1. Review the title, date, AM/PM, time zone, duration, server and announcement channel. Check your rendered preview.
2. Click Publish to Discord. Wait for the result at the bottom of the window. If it reports a partial failure, follow the message rather than creating a duplicate event.
3. Open Events and select the event's day on the calendar. Click View in the list below. Other members can use Refresh community events to find eligible published events.
4. Use Open in Discord to verify the post. Show announcement artwork displays the saved image; turn it off to see native event details. Click the image to enlarge it.
5. To change a published event, choose Edit event and then Save & Sync to Discord. This updates the existing event and announcement. Use Refresh artwork to retrieve the latest image.

**Remember:** Save local changes does not sync to Discord. Archive locally hides a local entry; it does not cancel the Discord event. Recurrence and signup groups are planning features, not automatic recurring posts or live role attendance.

## 6. Set your personal on-screen reminders

There are TWO switches: a master switch for popups and a personal choice on each event. Both must be enabled.

1. Open Reminders in the sidebar and check Enable on-screen event reminders.
2. Open Events, select the event's date, and click View. Drafts can also have personal reminders.
3. In the Event Details panel, find PERSONAL REMINDER and check Remind me on screen. This is the per-event checkbox that requests your notification.
4. Choose how early to notify you (for example, 15 minutes before). If the panel says popups are off, click Enable reminder popups.
5. You may close the main Event Horizon window. At the chosen time, a separate small window shows the countdown. Use Open event, Dismiss, or Snooze 5 minutes.
6. To stop one event's reminder, uncheck Remind me on screen or remove it on the Reminders page. To stop all popups, turn off the master switch.

**Remember:** Publishing, viewing an event, or adding it to Google Calendar does NOT enable this checkbox for you. The game and plugin must be running. Reminders use the saved schedule: refresh community events to receive changes. Popups stop ten minutes after the start or when the event ends, whichever comes first.

## 7. Connect your own Google Calendar

Google Calendar is optional. Each player connects their own account and chooses their own events; you do not need permission to edit the organizer's event.

1. Connect Discord first. Open Connection settings and expand Google Calendar (optional).
2. Click Refresh Google status. If available, choose Connect Google Calendar, then Open Google authorization.
3. Check the connection reference and sign in to the Google account you want to use. Complete the consent steps in the browser.
4. Return to Event Horizon and click Refresh Google status. If Prepare calendar appears, click it. Wait for Dedicated Event Horizon calendar ready.
5. Open an event in View, expand My Google Calendar in the details panel, and click Add to my Google Calendar.
6. Look for the dedicated Event Horizon calendar in Google Calendar. Configure notifications in Google Calendar if you also want phone/browser alerts. In Connection settings, Refresh my subscriptions lets you check each event's sync status.

**Remember:** This creates your personal calendar copy and follows organizer updates through the relay, even when the game is closed. Google edits do not change Discord. Remove from my calendar removes your copy only. On-screen game reminders still require the separate PERSONAL REMINDER checkbox.

## 8. If something doesn't work

Most setup issues can be resolved without recreating your event.

1. Discord connection expired: reconnect in Connection settings. Sessions last up to an hour; reloading the plugin requires linking again. Saved drafts and reminder choices remain.
2. No server/channel: verify the bot is installed and your account and the bot have the required event and channel permissions. Then Load servers / Refresh channels again.
3. No popup: check both Enable on-screen event reminders AND Remind me on screen for that event. Check the saved time zone, reminder lead time, and whether it was dismissed or snoozed. Normal game/Dalamud UI hiding can also hide windows.
4. Google unavailable or access denied: the relay operator may need to finish setup or allow your account during testing. Players do not need their own Google Cloud project.
5. Google event missing: confirm the dedicated calendar is ready and you clicked Add to my Google Calendar. Refresh Google status / Refresh my subscriptions; use Retry enabled event syncs if needed.
6. Image differs from your edits: save and sync the event, then Refresh artwork. Older publications may display a labelled reconstruction. Keep drafts and avoid deleting/recreating a publication to troubleshoot it.

**Remember:** Help & Walkthrough is always available from the sidebar. You can repeat any step without enabling reminders, authorizing accounts, or posting automatically.


## Personal events — only me

1. Choose **+ Create Event**, then **Visibility → Personal — only me**.
2. Enter your title, date, time, time zone and duration. Location is optional.
3. Choose **Save personal event**. It appears in your Events calendar and is never published to Discord or listed for other players.
4. Open **View → Personal Reminder**, check **Remind me on screen**, choose the lead time, and enable reminder popups if prompted. The game and plugin must be running for these popups.
5. For an optional Google copy, connect your account and Google Calendar in Connection settings. Use **Save & sync my Google Calendar** in the editor, or **My Google Calendar → Add / update my Google copy** in View. Details pass through the relay to your connected Google account. Google calendar sharing settings still apply.
6. After editing, use **Save & sync my Google Calendar** to update Google. Ordinary saving does not update Google. Check Google status in Connection settings for pending or failed work; **Retry enabled event syncs** retries private copies too.
7. **Remove my Google copy** removes the remote copy and keeps your local event. Archiving locally stops local reminders but keeps the Google copy. Remove the Google copy before archiving if desired.

### Optional Discord direct messages

In a saved personal event's View, expand **Discord DM reminders (optional)**. This starts off.

- Choose advance and near-start times, then check **Send me Discord reminders**. The recipient is your linked Discord account, never a server channel.
- To change times while enabled, choose **Apply DM reminder times**. Match the near-start time to your on-screen reminder if you want them together; they are independent settings. Equal times produce only one DM.
- **Send test reminder to me** sends a real test DM to your linked account. You may need to allow DMs and share a Discord server with the bot.
- The relay checks approximately once a minute and continues while you are out of game or your plugin login expires. Times already passed when scheduled are skipped. Outages can delay or prevent delivery; reminders more than ten minutes late are skipped.
- **Refresh DM delivery status** shows scheduled, sent, missed or failed/unconfirmed delivery. Uncertain sends are not automatically repeated, to avoid duplicate messages.
- Uncheck **Send me Discord reminders** to cancel unsent DMs. You must be connected to the same account and relay. Turn these off before archiving.
- Editing an event with DMs enabled also updates its bot schedule when saved and requires a connection. Google sync remains a separate explicit action.

Published/synced events keep their visibility. Use **Duplicate** to make a separate event with a different visibility. Personal events and popup preferences are stored in this plugin installation; they are not character-specific within the same installation. Signing out does not cancel opted-in remote reminders. No live test DM is sent automatically by an update.

## Leave editing without getting stuck

Choose **Cancel** or another sidebar page. A separate Event Horizon confirmation window offers **Discard changes** or **Keep editing**. Only the planner is disabled until you choose; the confirmation and other game windows stay interactive. Discard does not change the saved event.

## Delete a published community event

1. Connect the same Discord account that originally published the event, using its original relay. The organizer display name does not grant deletion rights.
2. Open the event in **View** and choose **Delete published event...**. You can also open an old event from History.
3. Read the confirmation, then choose **Delete everywhere**, or **Keep event** to cancel.
4. The relay withdraws the event from community listings, deletes its Discord announcement and scheduled event, and queues deletion of Google copies managed by Event Horizon for the organizer and subscribers.
5. Open the deletion record under **History** and choose **Check deletion status**. It reports outstanding Google copies. Use **Retry remaining cleanup** after resolving permissions or service failures.
6. Subscribers whose Google access was disconnected or revoked must reconnect the original Google account and choose **Retry enabled event syncs** in Connection settings. The relay cannot delete independently exported/copied entries or entries in an account it no longer has permission to access.

Deletion cannot be undone; duplicating the retained local record makes a new event. Ordinary **Archive locally** keeps remote copies and does not delete a Discord event. Partial deletion progress is retained and retries do not create new posts.
