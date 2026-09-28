# Event card composition

The existing .NET relay now supports Cloudflare Browser Run through its screenshot API. It builds an HTML/CSS card with bundled Cinzel, the Event Horizon icon, the uploaded banner, date/time, description, organizer, community, location and planned signup groups. Browser Run returns a PNG. The relay sends one PNG attachment and a real Discord event link button, and edits the same announcement on subsequent syncs. Attendance is not invented; live role signups remain future work.

## Enable on the existing relay

Set CARD_RENDERER=browser-run, CLOUDFLARE_ACCOUNT_ID to the Cloudflare account ID, and CLOUDFLARE_BROWSER_TOKEN to a secret API token scoped to Browser Rendering Edit for that account. Keep this token on the server. For local testing, Start-Relay.ps1 -ComposeCards prompts for these values without saving secrets.

The plugin continues to send event data and a banner to the authenticated relay. The relay sends only the generated card HTML, embedded font/icon, and banner to Cloudflare. Discord OAuth tokens and bot credentials are never included in the render request. Cloudflare Images is unnecessary for this composition path.

Default mode remains discord-embed until Browser Run is configured; /health reports the actual composition mode. There is no silent fallback after a rendering failure. Rendering occurs before Discord mutations, preserving the existing post when the browser quota or service is unavailable. Identical consecutive renders reuse the last result in memory; only one card is cached to bound memory use. Source banners persist with event records for edits after restart. Back up and protect the relay's data directory accordingly.

All client strings are HTML-encoded. Templates disable scripts, network requests, external images and external fonts with CSP; banners must be validated inline PNG/JPEG. Output is limited to 4 MB. The native scheduled event keeps the original banner as its cover; the announcement receives the composed card.

## Deployment boundary

The user selected Cloudflare for the whole relay. The port is now in cloudflare/, with a Browser Run binding and SQLite-backed Durable Object storage. See cloudflare/README.md for deployment and credential setup. Its Node tests pass; actual Wrangler bundling and deployment must be verified from the user's authenticated PowerShell because this agent environment blocks the build's filesystem traversal and Cloudflare API access. No public endpoint or credentials have been fabricated. The adjacent ASP.NET relay remains a local/reference implementation.

After a real shared relay is deployed and Discord configured, run Set-SharedRelay.ps1 with its HTTPS origin. It checks /health before updating the plugin's bundled default. Rebuild and distribute the plugin. Users receive a locked relay address; unchecking Lock relay address enables custom/local settings. Checking it again restores the bundled service if available.

Live validation still required: Discord OAuth, bot installation/channel permissions, a real Browser Run render, and Discord attachment delivery/editing. Automated checks cover the rendering boundary and failures with fake services, not actual remote accounts.

Reference: https://developers.cloudflare.com/browser-run/quick-actions/screenshot-endpoint/
