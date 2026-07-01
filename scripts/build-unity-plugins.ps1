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
