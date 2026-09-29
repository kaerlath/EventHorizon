# Publish 0.17.2

The plugin archive and local source are prepared. No new relay deployment is needed if relay 0.17.1 is already installed.

Run in PowerShell to publish the update:

```powershell
Set-Location "C:\Users\kaerl\Documents\Codex\EventHorizon"
git push origin main
if ($LASTEXITCODE -ne 0) { throw 'Git push failed. Stop here.' }
gh release create v0.17.2 '.\dist\EventHorizon-0.17.2-plugin.zip' --target main --title 'Event Horizon 0.17.2' --notes-file '.\docs\Release-0.17.2.md'
if ($LASTEXITCODE -ne 0) { throw 'Release creation failed. Check GitHub before retrying.' }
```

Refresh the Dalamud installer and update Event Horizon. For a development-plugin installation, unload/reload the plugin to load the rebuilt Debug DLL.

Check a crowded day in both month and week views, hover the smaller information icons, and select a capsule to open its details below. Appearance's calendar text size remains available for individual preferences.
