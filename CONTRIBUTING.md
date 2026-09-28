# Contributing to Event Horizon

Start with the player guide and README. Keep changes focused and explain what players will see differently.

## Local checks

```powershell
dotnet build src/EventHorizon.csproj -c Debug
dotnet run --project tests/EventHorizon.Checks.csproj
node --test relay/cloudflare/tests/*.test.js
```

Test interface changes in-game and publishing changes in a Discord test server. Automated tests do not replace live OAuth or Browser Run testing.

## Protect users and credentials

- Never commit Discord/Google tokens, OAuth secrets, Cloudflare credentials, `.env`, `.dev.vars`, local events.json, or account exports.
- Keep ownership and permission checks on the relay. Display names never grant editing rights.
- Preserve event/message IDs during updates. Do not repeat uncertain create operations automatically.
- Distinguish local saves, Discord publication, personal reminders, and Google subscriptions.
- Include licenses with shared fonts; do not redistribute private imported fonts.

Include the plugin version, reproduction steps, expected behavior and error text in bug reports. Do not include tokens or full OAuth callback URLs. Relay secrets belong in Wrangler secret prompts, not source files or GitHub issues.
