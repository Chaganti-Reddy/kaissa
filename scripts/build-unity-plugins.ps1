#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the shared core for .NET Standard 2.1 and copies the DLLs into the Unity client.

.DESCRIPTION
    Publishes Kaissa.Training (which pulls in Learning, Rules, Engine) for netstandard2.1 and copies
    the Kaissa assemblies plus the JSON/Channels dependencies Unity does not already ship into
    client/Assets/Plugins/Kaissa. Run this after changing the core so the Unity project picks it up.
#>
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot 'artifacts/unity-plugins'
$pluginDir = Join-Path $repoRoot 'client/Assets/Plugins/Kaissa'

Write-Host "Publishing core for netstandard2.1..."
dotnet publish (Join-Path $repoRoot 'src/Kaissa.Training') -c $Configuration -f netstandard2.1 -o $publishDir | Out-Null

# Our libraries + the deps Unity does NOT provide. The rest (System.Memory/Buffers/Unsafe/
# Numerics.Vectors/Threading.Tasks.Extensions) are shipped by Unity; copying them would conflict.
$wanted = @(
    'Kaissa.Learning.dll',
    'Kaissa.Chess.Engine.dll',
    'Kaissa.Chess.Rules.dll',
    'Kaissa.Training.dll',
    'System.Text.Json.dll',
    'System.Text.Encodings.Web.dll',
    'System.Threading.Channels.dll',
    'Microsoft.Bcl.AsyncInterfaces.dll'
)

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
foreach ($dll in $wanted) {
    Copy-Item (Join-Path $publishDir $dll) $pluginDir -Force
    Write-Host "  -> $dll"
}

Write-Host ""
Write-Host "Unity plugins updated in $pluginDir"

# Stage Stockfish into StreamingAssets for the play-vs-bot screen (desktop). Fetched by
# scripts/fetch-stockfish.ps1; not committed. iOS will need an embedded engine instead.
$engine = Get-ChildItem -Path (Join-Path $repoRoot 'third_party/stockfish') -Recurse -Filter 'stockfish*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -ne $engine) {
    $streaming = Join-Path $repoRoot 'client/Assets/StreamingAssets/stockfish'
    New-Item -ItemType Directory -Force -Path $streaming | Out-Null
    Copy-Item $engine.FullName (Join-Path $streaming 'stockfish.exe') -Force
    Write-Host "Stockfish staged in $streaming"
}
else {
    Write-Host "Stockfish not found under third_party/stockfish; run scripts/fetch-stockfish.ps1 first." -ForegroundColor Yellow
}

# Stage lc0 + the Maia nets (human-like opponent) into StreamingAssets. Fetched by
# scripts/fetch-lc0.ps1; not committed. Optional - the app runs without it (Maia bots are hidden).
$lc0 = Get-ChildItem -Path (Join-Path $repoRoot 'third_party/lc0') -Filter 'lc0.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -ne $lc0) {
    $lc0Dst = Join-Path $repoRoot 'client/Assets/StreamingAssets/lc0'
    New-Item -ItemType Directory -Force -Path (Join-Path $lc0Dst 'nets') | Out-Null
    Get-ChildItem -Path $lc0.Directory.FullName -File |
        Where-Object { $_.Extension -in '.exe', '.dll' } |
        ForEach-Object { Copy-Item $_.FullName $lc0Dst -Force }
    $netSrc = Join-Path $repoRoot 'third_party/lc0/nets'
    if (Test-Path $netSrc) { Copy-Item (Join-Path $netSrc '*.pb.gz') (Join-Path $lc0Dst 'nets') -Force }
    Write-Host "lc0 + Maia nets staged in $lc0Dst"
}
else {
    Write-Host "lc0 not found under third_party/lc0; run scripts/fetch-lc0.ps1 for the human-like opponent." -ForegroundColor Yellow
}
