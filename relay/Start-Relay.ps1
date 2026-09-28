param([string]$ApplicationId, [switch]$ComposeCards)
$ErrorActionPreference = 'Stop'
if (-not $ApplicationId) { $ApplicationId = Read-Host 'Discord application ID' }
if ($ApplicationId -notmatch '^\d+$') { throw 'Enter the numeric Discord application ID.' }
$clientSecret = Read-Host 'Discord OAuth2 client secret' -AsSecureString
$botSecret = Read-Host 'Discord bot token' -AsSecureString
$prior = @{}
$keys = @('DISCORD_CLIENT_ID','DISCORD_CLIENT_SECRET','DISCORD_BOT_TOKEN','PUBLIC_ORIGIN','EVENT_DATA_PATH','ASPNETCORE_URLS','CARD_RENDERER','CLOUDFLARE_ACCOUNT_ID','CLOUDFLARE_BROWSER_TOKEN')
foreach ($key in $keys) { $prior[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
try {
    if ($ComposeCards) {
        $env:CLOUDFLARE_ACCOUNT_ID = Read-Host 'Cloudflare account ID'
        $browserSecret = Read-Host 'Cloudflare API token with Browser Rendering Edit permission' -AsSecureString
        $env:CLOUDFLARE_BROWSER_TOKEN = [Net.NetworkCredential]::new('', $browserSecret).Password
        $env:CARD_RENDERER = 'browser-run'
    } else { $env:CARD_RENDERER = 'discord-embed' }
    $env:DISCORD_CLIENT_ID = $ApplicationId
    $env:DISCORD_CLIENT_SECRET = [Net.NetworkCredential]::new('', $clientSecret).Password
    $env:DISCORD_BOT_TOKEN = [Net.NetworkCredential]::new('', $botSecret).Password
    $env:PUBLIC_ORIGIN = 'http://localhost:5187'
    $env:EVENT_DATA_PATH = Join-Path $PSScriptRoot 'data/events.json'
    $env:ASPNETCORE_URLS = 'http://localhost:5187'
    Write-Host 'Starting Event Horizon at http://localhost:5187. Keep this window open; Ctrl+C stops the relay.'
    dotnet run --project (Join-Path $PSScriptRoot 'EventHorizon.Relay.csproj') -c Release --no-launch-profile
}
finally {
    foreach ($key in $keys) { [Environment]::SetEnvironmentVariable($key, $prior[$key], 'Process') }
    $clientSecret.Dispose()
    $botSecret.Dispose()
    if ($browserSecret) { $browserSecret.Dispose() }
}
