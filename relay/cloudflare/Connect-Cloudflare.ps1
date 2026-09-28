$ErrorActionPreference = 'Stop'
$priorPath = $env:PATH
$runtime = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies'
$bundledNode = Join-Path $runtime 'node/bin'
$bundledPnpm = Join-Path $runtime 'bin/fallback/pnpm.cmd'
try {
    if (Test-Path -LiteralPath (Join-Path $bundledNode 'node.exe')) { $env:PATH = "$bundledNode;$env:PATH" }
    if (Test-Path -LiteralPath $bundledPnpm) { $packageManager = $bundledPnpm }
    elseif (Get-Command pnpm.cmd -ErrorAction SilentlyContinue) { $packageManager = (Get-Command pnpm.cmd).Source }
    else { throw 'pnpm was not found. Install Node.js LTS and pnpm before continuing.' }
    Push-Location $PSScriptRoot
    try {
        Write-Host 'Installing the official Cloudflare deployment tool into this project...'
        & $packageManager install
        if ($LASTEXITCODE -ne 0) { throw 'Cloudflare tooling installation failed. No login or deployment was attempted.' }
        Write-Host 'Opening Cloudflare sign-in. Review and approve the Wrangler request in your browser.'
        & $packageManager exec wrangler login
        if ($LASTEXITCODE -ne 0) { throw 'Cloudflare sign-in did not complete.' }
        & $packageManager exec wrangler whoami
        if ($LASTEXITCODE -ne 0) { throw 'Could not verify Cloudflare access.' }
        Write-Host 'Cloudflare authentication verified. No relay has been deployed and no paid plan has been enabled.'
    }
    finally { Pop-Location }
}
finally { $env:PATH = $priorPath }
