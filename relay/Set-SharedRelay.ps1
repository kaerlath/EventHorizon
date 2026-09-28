param([Parameter(Mandatory)][uri]$Address)
$ErrorActionPreference = 'Stop'
if ($Address.Scheme -ne 'https' -or $Address.AbsolutePath -ne '/' -or $Address.UserInfo -or $Address.Query -or $Address.Fragment) {
    throw 'Use the deployed HTTPS origin without a path, query, or credentials.'
}
$health = Invoke-RestMethod -Uri ([uri]::new($Address, 'health')) -TimeoutSec 15 -MaximumRedirection 0
if ($health.service -ne 'Event Horizon' -or -not $health.configured) { throw 'The relay must identify as Event Horizon and have Discord configured.' }
$destination = Join-Path $PSScriptRoot '../assets/relay-defaults.json'
@{ RelayUrl = $Address.AbsoluteUri } | ConvertTo-Json | Set-Content -LiteralPath $destination -Encoding utf8NoBOM
Write-Host 'Verified shared relay saved. Rebuild the plugin to distribute this default.'
