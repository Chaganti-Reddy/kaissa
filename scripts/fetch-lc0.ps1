#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Downloads the Leela Chess Zero (lc0) engine and the Maia human-like networks into
    third_party/lc0 (git-ignored).

.DESCRIPTION
    lc0 is GPLv3 and Maia's weights are released by the CSSLab (maia-chess); neither is committed to
    this repository. This script fetches an official lc0 CPU build plus the maia-1100..1900 nets so the
    human-like opponent can run. The CPU (dnnl) build is used so no GPU is required. Re-run to update.

.PARAMETER Version
    lc0 release tag (default: v0.32.1).

.PARAMETER Asset
    lc0 release asset for the target platform (default: the Windows CPU dnnl build).
#>
param(
    [string]$Version = 'v0.32.1',
    [string]$Asset = "lc0-v0.32.1-windows-cpu-dnnl.zip"
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$targetDir = Join-Path $repoRoot 'third_party/lc0'
$netDir = Join-Path $targetDir 'nets'
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
New-Item -ItemType Directory -Force -Path $netDir | Out-Null

# curl.exe is used rather than Invoke-WebRequest: the latter throttles badly on the raw.githubusercontent
# net files (a full stall to a few KB/s), while curl fetches them in seconds.
function Fetch($url, $out) {
    Write-Host "Downloading $url"
    & curl.exe -L --fail --max-time 300 -o $out $url
    if ($LASTEXITCODE -ne 0) { throw "Download failed ($LASTEXITCODE): $url" }
}

$url = "https://github.com/LeelaChessZero/lc0/releases/download/$Version/$Asset"
$archive = Join-Path $targetDir $Asset
Fetch $url $archive
Write-Host "Extracting to $targetDir"
Expand-Archive -Path $archive -DestinationPath $targetDir -Force

$levels = 1100, 1200, 1300, 1400, 1500, 1600, 1700, 1800, 1900
foreach ($lvl in $levels) {
    $net = "maia-$lvl.pb.gz"
    Fetch "https://raw.githubusercontent.com/CSSLab/maia-chess/master/maia_weights/$net" (Join-Path $netDir $net)
}

$engine = Get-ChildItem -Path $targetDir -Recurse -Filter 'lc0.exe' | Select-Object -First 1
if ($null -eq $engine) { throw "Could not locate lc0.exe after extraction." }
Write-Host ""
Write-Host "lc0 ready: $($engine.FullName)"
Write-Host "Maia nets in: $netDir"