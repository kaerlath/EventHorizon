# Publish 0.17.3

The plugin archive and local source are prepared. No new relay deployment is needed if relay 0.17.1 is already installed.

Run in PowerShell to publish the update:

```powershell
Set-Location "C:\Users\kaerl\Documents\Codex\EventHorizon"
git push origin main
if ($LASTEXITCODE -ne 0) { throw 'Git push failed. Stop here.' }
gh release create v0.17.3 '.\dist\EventHorizon-0.17.3-plugin.zip' --target main --title 'Event Horizon 0.17.3' --notes-file '.\docs\Release-0.17.3.md'
if ($LASTEXITCODE -ne 0) { throw 'Release creation failed. Check GitHub before retrying.' }
```

Refresh the Dalamud installer and update Event Horizon. For a development-plugin installation, unload/reload the plugin to load the rebuilt Debug DLL.

Open Event type legend & filters. Try individual checkboxes, All off, and All on in both month and week views. Reload the plugin to confirm that the choices persist.
