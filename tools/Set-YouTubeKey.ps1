<#
.SYNOPSIS
    Stores the user's own YouTube Data API v3 key for Mimi.

.DESCRIPTION
    Mimi reads the YouTube key from the environment or from a file in the
    user's own data folder. This script is the terminal way to set it up, so
    the app itself never shows an API key field.

    The key is written to:
        %LOCALAPPDATA%\Mimi\youtube-api-key.txt

    The value is validated before it is written: surrounding whitespace is
    trimmed, and empty input, inner spaces or line breaks, non-ASCII
    characters (the smart quotes that web pages copy), and lengths outside
    20-100 characters are refused. Mimi applies the same rule when it reads
    the file, so a damaged file is treated as "no key" rather than being sent
    to Google and answered with a 400.

    Without -Key the script prompts, and the typed value is not echoed.

    The key is never printed in full. -Status shows only a masked hint.

.PARAMETER Key
    The API key. Omit it to be prompted instead.

.PARAMETER Remove
    Deletes the stored key file. The YouTube entry then disappears from Mimi.

.PARAMETER Status
    Reports whether a key is configured, without changing anything.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Set-YouTubeKey.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Set-YouTubeKey.ps1 -Status

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Set-YouTubeKey.ps1 -Remove
#>
#Requires -Version 5.1
[CmdletBinding(DefaultParameterSetName = 'Store')]
param(
    [Parameter(ParameterSetName = 'Store', Position = 0)]
    [string]$Key,

    [Parameter(ParameterSetName = 'Remove', Mandatory = $true)]
    [switch]$Remove,

    [Parameter(ParameterSetName = 'Status', Mandatory = $true)]
    [switch]$Status
)

$ErrorActionPreference = 'Stop'

$dataDirectory = Join-Path $env:LOCALAPPDATA 'Mimi'
$keyFile = Join-Path $dataDirectory 'youtube-api-key.txt'

function Test-KeyShape {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    if ($Value.Length -lt 20 -or $Value.Length -gt 100) { return $false }

    foreach ($character in $Value.ToCharArray()) {
        if ([int]$character -lt 0x21 -or [int]$character -gt 0x7E) { return $false }
    }

    return $true
}

function Get-MaskedHint {
    param([string]$Value)

    if ($Value.Length -le 8) { return '****' }
    return $Value.Substring(0, 4) + '****' + $Value.Substring($Value.Length - 2)
}

if ($Status) {
    Write-Host "Key file: $keyFile"

    if (-not (Test-Path -LiteralPath $keyFile)) {
        Write-Host 'No key file. YouTube stays hidden in Mimi.'
        exit 0
    }

    # Get-Content -Raw returns $null for an empty file; cast before trimming.
    $stored = ([string](Get-Content -LiteralPath $keyFile -Raw)).Trim()

    if (Test-KeyShape -Value $stored) {
        Write-Host "Key present: $(Get-MaskedHint -Value $stored) ($($stored.Length) characters)"
    }
    else {
        Write-Host 'The file exists but its contents are not a usable key.'
    }

    if (-not [string]::IsNullOrWhiteSpace($env:MIMI_YOUTUBE_API_KEY)) {
        Write-Host 'Warning: MIMI_YOUTUBE_API_KEY is set and takes precedence over this file.'
    }

    exit 0
}

if ($Remove) {
    if (Test-Path -LiteralPath $keyFile) {
        Remove-Item -LiteralPath $keyFile -Force
        Write-Host "Removed $keyFile"
    }
    else {
        Write-Host 'No key file to remove.'
    }

    if (-not [string]::IsNullOrWhiteSpace($env:MIMI_YOUTUBE_API_KEY)) {
        Write-Host 'MIMI_YOUTUBE_API_KEY is still set; remove it with:'
        Write-Host '  reg delete "HKCU\Environment" /v MIMI_YOUTUBE_API_KEY /f'
    }

    exit 0
}

if (-not $PSBoundParameters.ContainsKey('Key')) {
    $secure = Read-Host -AsSecureString 'YouTube Data API v3 key'
    $Key = [System.Net.NetworkCredential]::new('', $secure).Password
}

$Key = $Key.Trim()

if (-not (Test-KeyShape -Value $Key)) {
    # Write-Error would terminate here because ErrorActionPreference is Stop.
    $message = 'That does not look like an API key. ' +
        'Expected 20-100 printable ASCII characters, with no spaces or line breaks. ' +
        'Copy the whole value from Google Cloud Console.'
    Write-Host $message -ForegroundColor Red
    exit 2
}

New-Item -ItemType Directory -Path $dataDirectory -Force | Out-Null

# ASCII, no trailing newline: exactly what Mimi reads. The file is not hidden,
# because hiding it would only make the user unable to find it later.
$Key | Set-Content -LiteralPath $keyFile -NoNewline -Encoding Ascii

Write-Host "Saved key to $keyFile"
Write-Host 'Switch back to Mimi: the YouTube entry appears without a restart.'
Write-Host 'If MIMI_YOUTUBE_API_KEY is also set, the environment variable wins.'
