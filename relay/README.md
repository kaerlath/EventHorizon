# Event Horizon relay setup

This is the new relay prepared alongside your existing plugin. Nothing has been deployed or posted to Discord. Start locally first; a public host is optional while testing on your own computer.

## 1. Create the Discord application

Open https://discord.com/developers/applications and create **Event Horizon**.

1. Under OAuth2, add this exact redirect: `http://localhost:5187/auth/callback`.
2. Note the application ID and obtain the OAuth2 client secret.
3. Under Bot, obtain the bot token. Keep both secrets private; enter them only into the local startup prompts below.
4. Use the OAuth2 URL Generator with the `bot` scope. Grant **Create Events**, **View Channels**, **Send Messages**, and **Embed Links**, and **Attach Files**, then install the bot in your test server. Administrator permission and privileged Gateway intents are not required.
5. Your own Discord role must have Create Events (or Manage Events/Administrator). The announcement channel must permit both your account and the bot to view, send, embed links and attach files.

## 2. Start locally on Windows

Run `Start-Relay.ps1` from this folder using PowerShell. It prompts for the application ID and both secrets. Secret prompts are masked, and values exist only in the launched process environment. They are not saved to a project file. Keep the terminal open while using the plugin; Ctrl+C stops it. You will need the secrets again after a restart.

The script binds to `http://localhost:5187`. Health status is at `http://localhost:5187/health`. An unconfigured relay can run for health checks, but refuses new account-link requests.

## 3. Connect from FFXIV

1. Reload the development plugin, open `/eventhorizon`, then **Connect Discord**.
2. Uncheck **Lock relay address**, then click **Use local test relay**.
3. Click **Connect Discord account**, then **Open authorization page**.
4. Compare the reference shown by the browser to the plugin's reference, continue, and authorize through Discord.
5. Back in the plugin, click **Finish connection**, **Load servers**, select your server, then an optional announcement channel.
6. Create an event, fill in a future start, timezone, duration, world and location. Use **Preview**, then **Publish to Discord** when you are ready to send it.
7. To change a published event, edit it and use **Save & Sync to Discord**. **Save local changes** does not send changes.

## Hosting later

For composed single-image announcements and the current hosting boundary, see [COMPOSITION.md](COMPOSITION.md). Use `Start-Relay.ps1 -ComposeCards` to test with Cloudflare Browser Run credentials. Without that flag, the relay uses Discord embeds.

Publish with `dotnet publish EventHorizon.Relay.csproj -c Release -o publish`.
Run one relay instance under an account restricted to its data directory. Put it behind an HTTPS reverse proxy and configure its environment:

- `PUBLIC_ORIGIN`: exact public HTTPS origin without a path.
- `DISCORD_CLIENT_ID`, `DISCORD_CLIENT_SECRET`, `DISCORD_BOT_TOKEN`: application credentials from your host's secret manager.
- `EVENT_DATA_PATH`: absolute path on persistent storage, outside the public web root.
- `ASPNETCORE_URLS`: loopback HTTP listener for the trusted reverse proxy.

Add `https://YOUR-HOST/auth/callback` to the Discord application's redirect list, and enter `https://YOUR-HOST` in the plugin. Do not expose the internal HTTP port. Disable query-string logging at the proxy because OAuth callbacks carry short-lived authorization codes. The app does not trust forwarded client IP headers, so its rate limit is shared behind a proxy; configure additional per-client limits at the proxy.

## Data and recovery

`data/events.json` contains published event records, ownership, destinations and Discord IDs. It contains no bot or OAuth credentials. Back it up. A damaged data file stops relay startup rather than overwriting history. Use only one running relay process per data file.

The relay saves a pending create intent before calling Discord. If the connection is lost, it blocks a repeat create to avoid duplicate events. For reconciliation: stop the relay, back up the data file, inspect the affected server/channel, and either fill in the actual EventId/MessageId or confirm no object was created before clearing EventPending/MessagePending. Restart afterwards. Do not blindly clear pending flags.

Sessions and uncompleted links are memory-only. Sessions last up to an hour; pairing links expire in five minutes. Restarting the relay requires reconnecting. Remote history recovery returns only the signed-in user's events in servers where they still have event permissions.

## Validation and scope

Automated checks use a fake Discord HTTP service, including OAuth state consumption, permission revocation, channel overwrites, create/edit IDs, uncertain results across restart, history isolation, daylight saving validation, legacy data migration and failed-save preservation. Real Discord authorization and publication require your application setup. Custom RSVP buttons, recurring publishing and reminders are not included yet. PNG/JPEG banners now upload to both the scheduled event and announcement, with a 4 MB limit.

## 0.4 upload contract

Publishing now accepts a JSON object with Event (the event record) and BannerDataUrl. The banner is null to preserve an existing image, an empty string to remove it, or a PNG/JPEG data URL to upload/replace it. The plugin strips the local file path from the outgoing record. Announcement requests use multipart uploads, retain the returned attachment ID for later edits, and disable unsolicited mentions. The event button links to Discord's native event/Interested control. Planned signup groups are labelled; they are not live attendance.

