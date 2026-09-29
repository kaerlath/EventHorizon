# Install and publish 0.17.0

The local source and plugin archive are ready. These steps deploy the relay and publish the GitHub release; neither has been performed automatically.

First deploy the relay in PowerShell. This uses the project's installed Wrangler, avoiding the old missing pnpm path. The Node fallback below is the bundled runtime currently available on this computer.

```powershell
Set-Location "C:\Users\kaerl\Documents\Codex\EventHorizon\relay\cloudflare"
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    $ehNodeDirectory = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin'
    if (-not (Test-Path (Join-Path $ehNodeDirectory 'node.exe'))) { throw 'Install Node.js or add its directory to PATH, then try again.' }
    $env:PATH = "$ehNodeDirectory;$env:PATH"
}
& .\node_modules\.bin\wrangler.cmd deploy
if ($LASTEXITCODE -ne 0) { throw 'Relay deployment failed. Stop here.' }
Invoke-RestMethod 'https://event-horizon-relay.kaerlath.workers.dev/health'
```

Health should report version **0.17.0** and `configured: True`. Your existing secrets remain in Cloudflare. No new secrets or permissions are required.

Then publish the prepared plugin release:

```powershell
Set-Location "C:\Users\kaerl\Documents\Codex\EventHorizon"
git push origin main
if ($LASTEXITCODE -ne 0) { throw 'Git push failed. Stop here.' }
gh release create v0.17.0 '.\dist\EventHorizon-0.17.0-plugin.zip' --target main --title 'Event Horizon 0.17.0' --notes-file '.\docs\Release-0.17.0.md'
if ($LASTEXITCODE -ne 0) { throw 'Release creation failed. Check GitHub before retrying.' }
```

Refresh the Dalamud installer and update Event Horizon. For an existing development-plugin installation, unload/reload the plugin to load the rebuilt Debug DLL.

Test one continuous multi-day draft and one draft with a day gap between sessions. Confirm that the gap has no event, each scheduled day opens the same event, and the official entries have only View/source actions. Test Discord/Google publishing on the test server after the relay update. No test post or DM has been sent automatically.
