# Event Horizon

A Final Fantasy XIV Dalamud plugin for planning community events and sharing them with Discord.

Event Horizon combines an in-game calendar, local drafts, styled announcement images, personal reminders, and optional Google Calendar subscriptions. **Current plugin version: 0.16.1.** This is a friends-and-guild beta, not an official Dalamud plugin repository release.

## Start here

- **Players:** read the [step-by-step player guide](docs/Player-Guide.md), or open **Help & Walkthrough** inside the plugin.
- **Server administrators:** use **Connection settings → Add Event Horizon bot to a server**. Select the server in Discord and approve installation, then return to the plugin, connect your account, and click **Load servers**.
- **Contributors:** build the plugin using the instructions below.
- **Relay operators:** see [Cloudflare deployment](relay/cloudflare/README.md) and [Google Calendar setup](relay/cloudflare/GOOGLE-SETUP.md). Players using the shared relay do not need their own relay or developer applications.

## Features

- Private personal events with optional Google copies, in-game countdowns, and opt-in Discord DM reminders that run while the game is closed.
- Monthly/weekly calendar views, drafts, templates, history and event editing.
- Browser-based Discord linking and bot installation.
- Native Discord events and composed announcement images; Save & Sync updates existing posts.
- Banners, a shared font collection, custom font imports, and text effects.
- Saved announcement artwork in Event View with native editing controls.
- Optional personal on-screen countdown reminders while the main planner is closed.
- Personal Google Calendar subscriptions that follow organizer updates.
- Themes, animated highlights, reduced-motion settings, and an in-game walkthrough.

## Test the plugin

Once the v0.16.1 release package is published, install through Dalamud:

1. Open Dalamud Settings with `/xlsettings`, then the **Experimental** tab.
2. Add this URL under **Custom Plugin Repositories**, enable it, and save:
   `https://raw.githubusercontent.com/kaerlath/EventHorizon/main/repo.json`
3. Open `/xlplugins`, find **Event Horizon**, and install it.
4. Open `/eventhorizon` and follow **Help & Walkthrough**.

If you already use the development copy, disable that copy before enabling the installed version. Keep your existing plugin configuration.

For development, keep the DLL, manifest, icon, relay-defaults.json and fonts directory together. Add the EventHorizon.dll path in Dalamud's development-plugin settings and scan/load development plugins. The GitHub project page itself is not the installer feed; use the raw repo.json URL above.

The packaged shared service is `https://event-horizon-relay.kaerlath.workers.dev/`. This is public configuration, not a credential. Service availability and Google authorization eligibility depend on the operator's beta configuration.

## Build on Windows

Requirements: Git, .NET 10 SDK, and a local Dalamud API 15 development environment compatible with `Dalamud.NET.Sdk/15.0.0`. Node.js runs the Cloudflare tests; pnpm manages relay deployment tooling.

From the repository root:

```powershell
dotnet build src/EventHorizon.csproj -c Debug
dotnet run --project tests/EventHorizon.Checks.csproj
node --test relay/cloudflare/tests/*.test.js
```

The development DLL is `src/bin/Debug/EventHorizon.dll`. Use `-c Release` for a release build.

Cloudflare deployment prepares the shared font assets automatically. Fonts render remotely; local system fonts are not uploaded automatically. See [font library details](relay/cloudflare/FONT-LIBRARY.md).

## Project layout

| Directory | Purpose |
|---|---|
| `src/` | Windows Dalamud plugin and interface |
| `relay/cloudflare/` | Active Cloudflare relay, Discord/Google integrations and Browser Run composition |
| `relay/` | Earlier .NET relay for reference/local checks; it lacks some current Cloudflare features |
| `assets/` | Icon, shared relay address, fonts and font licenses |
| `tests/` | .NET checks for event rules and local state |
| `docs/` | Player documentation |

## Current limits

- On-screen reminders require the game/plugin to remain running. They use saved schedules; refresh community events for organizer changes. Google subscriptions run separately through the relay.
- Live role attendance, capacity enforcement, automated recurring publication are not implemented. Signup/recurrence fields are planning information.
- The publisher can delete a community event and its announcement, with queued removal of Event Horizon-managed Google copies. Disconnected Google accounts must reconnect for cleanup; independently copied/exported entries are outside relay control.
- Archiving locally does not cancel a Discord event. Saving local changes does not publish them.
- Stay connected while playing keeps Discord linked until game logout or plugin unload, with a 15-minute expiry after lost contact. Disable it to use sessions of up to one hour. Reconnect each play session or after revoked authorization. Organizer display names do not transfer editing rights.
- The relay is designed for a small beta. Uncertain Discord creates may need operator reconciliation to prevent duplicates.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Include the plugin version, reproduction steps and error text in reports. Never post credentials, tokens, full OAuth callback URLs or local account/configuration files.

## Fonts and licensing

Bundled fonts are unmodified and retain their upstream licenses beside the files. Cinzel and most shared families use SIL OFL; Special Elite uses Apache 2.0. `assets/fonts/shared/catalog.json` records their sources. Imported private fonts are not part of this repository.

A general license for the project's original code/artwork has not yet been selected. Third-party font licenses apply independently to their respective files.
