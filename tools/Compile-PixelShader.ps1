[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [string]$Profile = "ps_3_0",

    [string]$EntryPoint = "main"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Add-Candidate {
    param(
        [System.Collections.Generic.List[string]]$Candidates,
        [string]$Path
    )

    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        $Candidates.Add($Path)
    }
}

$candidates = [System.Collections.Generic.List[string]]::new()
Add-Candidate $candidates $env:FXC_PATH

$fxcCommand = Get-Command fxc.exe -ErrorAction SilentlyContinue
if ($null -ne $fxcCommand) {
    Add-Candidate $candidates $fxcCommand.Source
}

$programFilesX86 = [Environment]::GetFolderPath("ProgramFilesX86")
if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
    $windowsKitsBin = Join-Path $programFilesX86 "Windows Kits\10\bin"
    if (Test-Path -LiteralPath $windowsKitsBin) {
        Get-ChildItem -LiteralPath $windowsKitsBin -Directory |
            Sort-Object Name -Descending |
            ForEach-Object {
                Add-Candidate $candidates (Join-Path $_.FullName "x64\fxc.exe")
                Add-Candidate $candidates (Join-Path $_.FullName "x86\fxc.exe")
            }
    }
}

$fxc = $candidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($fxc)) {
    throw "Could not find fxc.exe. Install the Windows SDK or set FXC_PATH to its full path."
}

$inputFullPath = (Resolve-Path -LiteralPath $InputPath).Path
if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
} else {
    $outputFullPath = [System.IO.Path]::GetFullPath(
        (Join-Path (Get-Location).Path $OutputPath))
}

$outputDirectory = Split-Path -Parent $outputFullPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    [void](New-Item -ItemType Directory -Path $outputDirectory -Force)
}

Write-Host "Using shader compiler: $fxc"
& $fxc /nologo /T $Profile /E $EntryPoint /Fo $outputFullPath $inputFullPath
if ($LASTEXITCODE -ne 0) {
    throw "fxc.exe failed with exit code $LASTEXITCODE."
}

$outputFile = Get-Item -LiteralPath $outputFullPath
if ($outputFile.Length -le 0) {
    throw "fxc.exe produced an empty shader file: $outputFullPath"
}

Write-Host "Compiled $inputFullPath -> $outputFullPath ($($outputFile.Length) bytes)"
