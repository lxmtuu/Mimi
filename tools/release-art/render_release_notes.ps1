<#
.SYNOPSIS
    Turn docs/release/RELEASE-BODY.md into the notes published on a GitHub release.

.DESCRIPTION
    A release body on GitHub is rendered as a page of its own, so links written
    for the repository ("../../blob/main/SECURITY.md") and image paths relative
    to the repository ("docs/assets/release/banner-vi.png") both break there.

    This rewrites them for the release page:
      * image paths become raw.githubusercontent.com URLs that always point at
        the default branch, so the picture survives even if the file moves;
      * repository links become absolute github.com URLs for the tagged commit's
        branch (main), so they never 404 from the release page;
      * the banner and the "what is new" panel are placed where the markers say.

    The repository copy keeps its relative links, so it still works inside the
    repository. One source, two renderings.

.PARAMETER Tag
    Release tag, for example v0.7.0.

.PARAMETER Version
    Version inside the tag, for example 0.7.0.

.PARAMETER Source
    Markdown file to read. Default: docs/release/RELEASE-BODY.md

.PARAMETER Output
    Where to write the result. Default: dist/release-notes.md
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$Version,
    [string]$Source = 'docs/release/RELEASE-BODY.md',
    [string]$Output = 'dist/release-notes.md',
    [string]$Repository = 'lxmtuu/Mimi',
    [string]$Branch = 'main'
)

$ErrorActionPreference = 'Stop'
$raw = "https://raw.githubusercontent.com/$Repository/$Branch"
$web = "https://github.com/$Repository"

if (-not (Test-Path -LiteralPath $Source)) {
    throw "Release body not found: $Source"
}

$body = Get-Content -LiteralPath $Source -Raw

# A leading HTML comment is authoring notes, not something to publish.
$body = [regex]::Replace($body, '(?s)\A\s*<!--.*?-->\s*', '')

# Drop the per-release copy's own commentary, if it has any.
$body = [regex]::Replace($body, '(?s)\n<!--.*?-->', '')

# Images: repository paths -> raw URLs on the default branch.
$body = [regex]::Replace(
    $body,
    '!\[([^\]]*)\]\(docs/([^)\s]+)\)',
    { param($match) "![$($match.Groups[1].Value)]($raw/docs/$($match.Groups[2].Value))" })

# Repository links: ../../blob/main/x -> absolute.
$body = [regex]::Replace(
    $body,
    '\]\(\.\./\.\./blob/main/([^)]+)\)',
    { param($match) "]($web/blob/$Branch/$($match.Groups[1].Value))" })

# Release download links are relative inside the repository, absolute on the page.
$body = [regex]::Replace(
    $body,
    '\]\(\.\./\.\./releases/download/([^)]+)\)',
    { param($match) "]($web/releases/download/$($match.Groups[1].Value))" })

if ($body -match '\$\(\{') {
    throw 'Unsubstituted placeholder in the release body.'
}

$directory = Split-Path -Parent $Output
if ($directory -and -not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

# GitHub renders UTF-8. Write it as UTF-8 without a BOM, LF endings.
$text = $body -replace "`r`n", "`n"
[System.IO.File]::WriteAllText(
    (Join-Path (Get-Location) $Output),
    $text,
    [System.Text.UTF8Encoding]::new($false))

$bytes = (Get-Item -LiteralPath $Output).Length
Write-Host "Release notes for $Tag written to $Output ($bytes bytes)"
