# Event Horizon Cloudflare relay

This Worker is the Cloudflare port of the adjacent .NET relay. It retains the plugin's endpoint contract and is part of the same Event Horizon project. Browser Run uses a Worker binding, so no separate Cloudflare rendering token is needed. The .NET implementation is kept for local development and reference; the plugin points to one relay, not both.

## First deployment from your authenticated PowerShell

Run these from this folder, with `$ehPnpm` set to the installed pnpm.cmd as in the setup instructions:

```powershell
& $ehPnpm test
& $ehPnpm exec wrangler deploy --dry-run
```

If both succeed, deploy:

```powershell
& $ehPnpm exec wrangler deploy
```

The Worker initially exposes only an unconfigured health response. Auth/publish fail closed until all four settings below exist. Copy the workers.dev HTTPS origin printed after deployment; do not invent the subdomain. No paid plan is enabled by these commands. If Cloudflare asks for a plan change, stop and assess it first.

## Create the Discord application

At https://discord.com/developers/applications create **Event Horizon**. Keep the application ID available. Under OAuth2, register `https://YOUR-DEPLOYED-ORIGIN/auth/callback`. Obtain the OAuth2 client secret and bot token in the Discord portal, and enter each only at the corresponding masked Wrangler prompt:

```powershell
& $ehPnpm exec wrangler secret put PUBLIC_ORIGIN
& $ehPnpm exec wrangler secret put DISCORD_CLIENT_ID
& $ehPnpm exec wrangler secret put DISCORD_CLIENT_SECRET
& $ehPnpm exec wrangler secret put DISCORD_BOT_TOKEN
```

PUBLIC_ORIGIN is just the deployed HTTPS origin, no callback path. These commands persist settings in Cloudflare, never in plugin files or source. The OAuth2 URL Generator's bot scope should request Create Events, View Channels, Send Messages, Embed Links and Attach Files. Install into a test server where you have event-creation permission. Administrator and privileged Gateway intents are unnecessary.

Verify `/health` returns service Event Horizon, configured true and composition browser-run. Then use the parent Set-SharedRelay.ps1 to verify and bundle the address, or let the development session do that from the verified origin. The plugin must be rebuilt so its default address is distributed.

## Architecture and state

Worker → one SQLite-backed Durable Object → Discord API + Browser Run binding. The Durable Object serializes requests for this small beta, with a bounded queue and per-IP in-memory throttling. This intentionally favors correctness over high throughput; it is not a multi-tenant service at large scale yet. Cloudflare observes network requests as the host. Request logging is disabled to avoid recording OAuth callback codes.

Event ownership, event/message IDs and pending creation intents persist. Source images are retained in bounded chunks for editing after restart. Rendering happens before Discord writes. A lost create response blocks automatic duplicate creation; it needs operator reconciliation against Discord. There is no unsafe public reconciliation endpoint. Production rollout needs an authenticated operator recovery/export tool before expanding beyond controlled testing.

OAuth polling links expire after five minutes. Plugin 0.15.0 adds **Stay connected while playing**, enabled by default. While logged into FFXIV it sends an authenticated check-in every two minutes, renewing a 15-minute relay lease even with the planner/game UI hidden. Discord access tokens are renewed with the session's refresh token when near expiry; rotated refresh tokens replace the old value. Credentials remain in expiring relay state and are never returned to the plugin or written to its configuration. Revoked authorization requires a fresh login. Temporary renewal errors retry while the lease remains valid.

Logout/unload clears local authentication and attempts a bounded relay logout; crash/network loss falls back to lease expiry. Disabling renewal restores up to one hour, bounded by Discord token expiry. Old plugin sessions retain their one-hour behavior. Deploy this relay before installing plugin 0.15.0. Existing connections made before this update may need one fresh Discord authorization to obtain a refresh token. The local .NET development relay does not implement the new heartbeat endpoint. Offline Google and DM jobs are independent of the plugin session.

The generated HTML embeds Cinzel, selected shared/imported fonts, the existing icon and validated banner data. Text is HTML-encoded and CSP blocks outside resources; one fixed nonce-authorized script waits for embedded fonts before capture. One PNG is attached to the announcement; the original banner is the native event cover. Native event/Interested link buttons remain interactive beneath the image. Live role attendance, reminder scheduling and recurring publication are not implemented.

## Validation and limits

Run `pnpm test` for Node tests with fake Discord, storage and Browser Run. These cover pairing/state replay, ownership, permission revocation, image preservation/removal, UTC/DST conversion, rendering failures and uncertain creates across restarts. `wrangler deploy --dry-run` validates actual bundling. Real deployment, OAuth, Browser Run output and Discord create/edit must still be tested with your account.

The initial deployment does not migrate any locally hosted relay data. Keep the old relay and its history until a separate migration is planned. Existing drafts in the plugin are unaffected. The shared origin is not filled in until a real configured endpoint has been verified.

References:
- https://developers.cloudflare.com/browser-run/reference/browser-binding-api/
- https://developers.cloudflare.com/browser-run/quick-actions/screenshot-endpoint/
- https://developers.cloudflare.com/durable-objects/best-practices/access-durable-objects-storage/


## Appearance and typography (0.6.0)

### Card-first announcements (relay 0.15.2)

Announcements include the full description, title, start/end times, location and organizer as readable Discord message text, alongside the complete styled poster. The poster restores the full-width banner, every description paragraph with its selected styling, detail panel and planned signup groups. Capture grows to fit the content rather than clipping it to a compact excerpt. The banner fits without cropping. Components V2 orders the PNG attachment in a Media Gallery first, the complete readable text second, and the event link button last, all within one message. Discord still controls its preview dimensions; no larger display size is guaranteed.

Deploy the Worker, then use **Save & Sync to Discord** on an existing event to replace its announcement in place and clear its old text/embeds. Converted messages use Discord Components V2 permanently; future updates must preserve that format. Existing posts keep their prior layout until synced. The plugin's rendered preview uses the same complete artwork. This relay-only update requires no new plugin DLL.

The plugin's Appearance page saves five themes, a custom accent, border glow, reduced motion, animation speed and banner height locally. These preferences do not change other users' windows or the published announcement.

Each event stores independent title and description typography, with optional overrides for paragraphs separated by blank lines. Overrides follow paragraph positions; review them after rearranging text. Controls include size, bold, italic, underline, strike, color, alignment, outline, glow and letter/line spacing. Version 0.9 adds 18 shared font families and account-owned font imports; see FONT-LIBRARY.md. Browser font fallbacks remain for the three generic families. Local Windows font installations are not uploaded automatically. The native Discord event keeps plain text.

Deploy this Worker before using typography or the Render announcement preview button. Authenticated POST /preview returns the same PNG rendering used for publication without creating an event or message. Reading a retained banner requires publication ownership and current server access. Styles are bounded structured data, never raw HTML/CSS. The original local .NET relay does not provide this preview endpoint or the new typography rendering.

After deployment, reload plugin 0.6.0, reconnect if needed, edit a test event, expand Announcement typography & effects, and render a preview. Save & Sync updates the existing test announcement once satisfied. In-game and Browser Run visual verification remains part of the test-server check.

## Google Calendar (0.7.0)

See [GOOGLE-SETUP.md](GOOGLE-SETUP.md) for the Google Cloud project, consent screen, exact callback and PowerShell setup. Google is optional; missing Google configuration does not disable Discord publishing. The Worker now also forwards the announcement preview endpoint correctly.

## Personal calendar subscriptions (0.8.0)

Players can browse visible events through GET /events/community and manage their own Google copies through POST/DELETE /google/subscriptions/{eventId}. GET /google/subscriptions returns only the caller's subscription status. Membership and announcement-channel View Channel permissions are checked before disclosure and again before each background update. Subscriber tokens and deletion jobs are bound to the caller's dedicated Google calendar. No edit/publish authority is granted by subscribing.

See GOOGLE-SETUP.md for the two-account test and remaining beta limitations. Existing credentials and scopes remain valid; deploy the Worker update and reload the plugin.

## Event artwork (0.10.0)
Successful announcement PNGs are retained in chunked Durable Object storage. Authenticated GET /events/{id}/image rechecks current Discord membership and channel visibility before returning the PNG. Older publications are reconstructed from saved event data and explicitly labelled as previews, without posting to Discord. New announcements return the actual published bytes. Client Event View loads once on selection; Refresh artwork fetches updates. The native details view remains available.
