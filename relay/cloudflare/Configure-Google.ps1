param([string]$Pnpm = 'C:\Users\kaerl\.cache\codex-runtimes\codex-primary-runtime\dependencies\bin\fallback\pnpm.cmd')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
if (-not (Test-Path -LiteralPath $Pnpm)) { $Pnpm = (Get-Command pnpm.cmd -ErrorAction Stop).Source }

function Save-WorkerSecret([string]$Name, [string]$Value) {
    $Value | & $Pnpm exec wrangler secret put $Name
    if ($LASTEXITCODE -ne 0) { throw "Could not save $Name. Stop here and check Wrangler authentication." }
}

Write-Host 'Create the Web application OAuth client first, using the callback in GOOGLE-SETUP.md.'
$googleClientId = (Read-Host 'Google OAuth client ID').Trim()
if (-not $googleClientId.EndsWith('.apps.googleusercontent.com')) { throw 'Enter the OAuth client ID, not the project ID or relay URL.' }
$googleSecret = Read-Host 'Google OAuth client secret (hidden)' -AsSecureString
$googleSecretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($googleSecret)
try {
    Save-WorkerSecret 'GOOGLE_CLIENT_ID' $googleClientId
    Save-WorkerSecret 'GOOGLE_CLIENT_SECRET' ([Runtime.InteropServices.Marshal]::PtrToStringBSTR($googleSecretPointer))
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($googleSecretPointer)
    $googleSecret.Dispose()
}

$googleSecretInventory = & $Pnpm exec wrangler secret list
if ($LASTEXITCODE -ne 0) { throw 'Could not check existing secret names. Encryption key was not changed.' }
$googleExistingSecrets = ($googleSecretInventory -join "`n") | ConvertFrom-Json
if ($googleExistingSecrets.name -notcontains 'GOOGLE_TOKEN_KEY') {
    $googleKeyBytes = New-Object byte[] 32
    $googleRng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $googleRng.GetBytes($googleKeyBytes)
        $googleKey = -join ($googleKeyBytes | ForEach-Object { $_.ToString('x2') })
        Save-WorkerSecret 'GOOGLE_TOKEN_KEY' $googleKey
    } finally {
        [Array]::Clear($googleKeyBytes, 0, $googleKeyBytes.Length)
        $googleKey = $null
        $googleRng.Dispose()
    }
} else { Write-Host 'Keeping the existing Google token encryption key.' }

Write-Host 'Google secrets are configured. Deploy the prepared update with:'
Write-Host '& $ehPnpm exec wrangler deploy'
