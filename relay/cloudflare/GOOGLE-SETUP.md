# Event Horizon: optional Google Calendar setup

Plugin and Worker version: 0.8.0. The source and local tests are ready; a real Google sign-in and Calendar API test still require your Google Cloud credentials and deployment.

## 1. Create the Google project and consent screen

Open https://console.cloud.google.com/ and create or choose the Event Horizon project. Enable **Google Calendar API** under APIs & Services → Library.

In **Google Auth platform**, configure Branding and Audience. For accounts outside a single Google Workspace organization, choose External. Start in Testing and add your own Google account under Test users. Set the app name to Event Horizon and provide your support/contact email. For a public release, finish the applicable Google verification and privacy-policy requirements before expanding access.

Under Data Access, request these scopes only:

- `openid`
- `https://www.googleapis.com/auth/userinfo.email`
- `https://www.googleapis.com/auth/calendar.app.created`

The calendar scope allows creating and managing application-created secondary calendars. This implementation creates its own Event Horizon calendar and never requests access to the user's primary calendar. The account identity is used to reuse the right calendar after reconnection. Google Testing-mode authorizations with these scopes can expire after seven days; this is distinct from the plugin's Discord session duration.

## 2. Create browser sign-in credentials

Under Clients, create an OAuth client of type **Web application**. Register this exact Authorized redirect URI:

```
https://event-horizon-relay.kaerlath.workers.dev/google/callback
```

Save the client ID and secret. Do not put the secret in chat, source control, or a screenshot.

## 3. Configure the Worker through PowerShell

Run the prepared setup script from PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\kaerl\Documents\Codex\EventHorizon\relay\cloudflare\Configure-Google.ps1"
```

This one invocation can run the setup without changing your permanent execution policy. It prompts for the client ID and a hidden client secret, uploads them with Wrangler, and creates a random Google token encryption key only if none exists. It does not deploy. Keep an existing GOOGLE_TOKEN_KEY: replacing it makes stored Google connections unreadable.

In your existing authenticated PowerShell session:

```powershell
Set-Location 'C:\Users\kaerl\Documents\Codex\EventHorizon\relay\cloudflare'
& $ehPnpm exec wrangler deploy
Invoke-RestMethod 'https://event-horizon-relay.kaerlath.workers.dev/health'
```

The health response should report version `0.8.0` and `googleCalendar: True`. That confirms configuration is present, not that Google has accepted the credentials.

## 4. Test with your account

1. Reload the development plugin. Connect Discord if necessary.
2. Open Connection settings → Google Calendar (optional) → Refresh Google status.
3. Choose Connect Google Calendar, open the authorization page, verify the displayed reference matches the plugin, and sign into your test Google account.
4. Return to the plugin, refresh status, and select Prepare calendar. This creates a dedicated Event Horizon calendar, or reuses the recorded calendar for this account.
5. In Events, click Refresh community events, select a day, view a published event, and click Add to my Google Calendar. This requires visibility in Discord, not organizer or event-edit permission.
6. Allow roughly a minute for the queued update. Refresh Google status and check Google Calendar. Confirm title, time, location and Discord link.
7. Change the event title/time in the plugin and Save & Sync again. Confirm the same Google event updates without duplicates.
8. Click Remove from my calendar: only your Google copy is deleted, with retries if needed. Google Settings lists subscriptions and lets you remove or resume them. Disconnect leaves existing Google entries in place and pauses subscriptions until you explicitly resume them.

## Behavior and limits

- Personal subscriptions synchronize any published Event Horizon event you can view in Discord, one-way to your dedicated calendar. Server membership and announcement-channel visibility are checked before browsing, subscribing, and each background update. Events published by other tools and private Google appointments are not imported.
- Each event is opt-in per player. Organizer edits through Save & Sync update active subscribers. Subscribers cannot edit the original. Local drafts, local archive actions and local-only saves do not change Google. External Discord cancellations/deletions are not yet monitored.
- The relay stores Google tokens encrypted and refreshes them as needed. Your game does not need to remain open for queued updates. Disconnect deletes the relay's tokens and requests Google revocation; if revocation fails, the plugin tells you to remove app access in Google account settings.
- Google failures do not undo Discord publication. The relay retries up to six times with increasing delays. Settings shows pending/failed counts. After resolving the cause or reconnecting, choose Resume / retry for your subscription, or Retry enabled event syncs.
- Updates use stable Google event IDs. If creating the dedicated calendar itself times out after Google may have accepted it, creation is deliberately blocked to avoid another calendar. Operator reconciliation is required in this unusual case. Do not delete the relay's calendar mapping to blindly retry.
- Reconnecting a different Google account does not transfer existing copies. Explicitly adding or resuming an event sends it to the new account; old copies remain. To remove a copy from the original account, reconnect that account. Losing Discord visibility pauses further updates; the existing copy can still be removed through My subscriptions.
- The native Google event uses text and the Discord link, not the styled announcement PNG. Google default reminders apply; the plugin does not add guests or send invitations.

Official references:

- https://developers.google.com/workspace/calendar/api/auth
- https://developers.google.com/workspace/calendar/api/v3/reference/calendars/insert
- https://developers.google.com/workspace/calendar/api/v3/reference/events/insert
- https://developers.google.com/identity/protocols/oauth2/web-server


## Testing personal subscriptions (0.8.0)

Deploy the updated Worker and reload the updated plugin on both test accounts. No new Google scopes or secrets are required. While the Google app remains in Testing, add every test player's Google address to its Test users list.

Use a second Discord member without Create/Manage Events permissions. Connect its own Google account and prepare its dedicated calendar. Refresh community events, view the organizer's test event, and Add to my Google Calendar. Confirm it appears in that player's calendar. Change the event as the organizer and Save & Sync; confirm both copies update. Remove the second player's copy and confirm the organizer's copy and Discord event remain. Deny View Channel on the announcement channel and confirm browsing/subscribing is denied and later background updates pause.

Community events are session-only in the plugin and cleared when the Discord account/relay changes. Refresh community events to retrieve current titles, dates and details. Original banner files are not downloaded into the viewer's computer in this version. The relay remains the authority for editing rights even if a client is modified.
