<#
.SYNOPSIS
    Builds the Translation Tags plugin and installs it into the local Jellyfin plugins folder.
#>
param(
    [string]$JellyfinServerDir = "C:\Program Files\Jellyfin\Server",
    [string]$PluginsDir = "C:\ProgramData\Jellyfin\Server\plugins",
    [string]$Version = "1.0.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Building..." -ForegroundColor Cyan
dotnet build "$root\Jellyfin.Plugin.TranslationTags.csproj" -c Release -p:JellyfinServerDir="$JellyfinServerDir"
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$dll  = Join-Path $root "bin\Release\net10.0\Jellyfin.Plugin.TranslationTags.dll"
$meta = Join-Path $root "meta.json"
$dest = Join-Path $PluginsDir "Translation Tags_$Version"

Write-Host "Installing to $dest" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item $dll  $dest -Force
Copy-Item $meta $dest -Force

try {
    icacls $dest /grant "NETWORK SERVICE:(OI)(CI)F" /T | Out-Null
} catch {
    Write-Warning "Could not set NETWORK SERVICE permissions (may require admin): $_"
}

Write-Host "Done. Restart Jellyfin to load the plugin:" -ForegroundColor Green
Write-Host "    Restart-Service JellyfinServer -Force" -ForegroundColor Green
