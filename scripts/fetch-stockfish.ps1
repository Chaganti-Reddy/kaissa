#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Downloads a Stockfish build into third_party/stockfish (git-ignored).

.DESCRIPTION
    Stockfish is GPLv3 and is not committed to this repository. This script fetches an official
    release build so the engine spike, tests, and CLI can run. Re-run to update the pinned version.

.PARAMETER Version
    Stockfish release tag (default: sf_18).

.PARAMETER Asset
    Release asset file name for the target platform (default: the generic Windows x86-64 build).

.EXAMPLE
    ./scripts/fetch-stockfish.ps1
    ./scripts/fetch-stockfish.ps1 -Asset stockfish-ubuntu-x86-64.tar
#>
param(
    [string]$Version = 'sf_18',
    [string]$Asset = 'stockfish-windows-x86-64.zip'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$targetDir = Join-Path $repoRoot 'third_party/stockfish'
$url = "https://github.com/official-stockfish/Stockfish/releases/download/$Version/$Asset"

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
$archive = Join-Path $targetDir $Asset

Write-Host "Downloading $url"
Invoke-WebRequest -Uri $url -OutFile $archive

Write-Host "Extracting to $targetDir"
if ($Asset.EndsWith('.zip')) {
    Expand-Archive -Path $archive -DestinationPath $targetDir -Force
}
else {
    tar -xf $archive -C $targetDir
}

$engine = Get-ChildItem -Path $targetDir -Recurse -Include 'stockfish*' |
    Where-Object { -not $_.PSIsContainer -and $_.Extension -ne '.zip' -and $_.Extension -ne '.tar' } |
    Select-Object -First 1

if ($null -eq $engine) {
    throw "Could not locate the Stockfish binary after extraction."
}

Write-Host ""
Write-Host "Stockfish ready: $($engine.FullName)"
Write-Host "Set the environment variable so the tests and CLI can find it:"
Write-Host "  `$env:KAISSA_STOCKFISH_PATH = '$($engine.FullName)'"
