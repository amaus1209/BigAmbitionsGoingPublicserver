<#
  build-windows.ps1 - build Going Public and install it into ModsLocal.
  Also moves an old separate "BA Telemetry Bridge" mod out of ModsLocal into ModsLocal-Backup (nothing is deleted).

  USAGE (PowerShell, in the repo folder):
    powershell -ExecutionPolicy Bypass -File .\build-windows.ps1
    ... -Dev                 build the Dev configuration (diagnostics on)
    ... -GameDir "D:\SteamLibrary\steamapps\common\Big Ambitions"
    ... -NoBuild             only look for the game and the old bridge
    ... -DryRun              show what would happen, change nothing

  Needs the .NET SDK once:  winget install Microsoft.DotNet.SDK.8
#>
[CmdletBinding()]
param([switch]$Dev, [string]$GameDir = '', [switch]$NoBuild, [switch]$DryRun)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$config = if ($Dev) { 'Dev' } else { 'Release' }
if ($DryRun) { $NoBuild = $true }

function Say($t) { Write-Host "==> $t" -ForegroundColor Cyan }

# ── Steam libraries: registry + libraryfolders.vdf ─────────────────────────────────────────────────────────
function Get-SteamLibraries {
    $roots = @()
    foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam') {
        try {
            $p = Get-ItemProperty -Path $key -ErrorAction Stop
            foreach ($n in 'SteamPath', 'InstallPath') { if ($p.$n) { $roots += ($p.$n -replace '/', '\') } }
        } catch { }
    }
    $roots += "${env:ProgramFiles(x86)}\Steam"
    $libs = New-Object System.Collections.Generic.List[string]
    foreach ($r in $roots | Select-Object -Unique) {
        if (-not (Test-Path (Join-Path $r 'steamapps'))) { continue }
        if (-not $libs.Contains($r)) { $libs.Add($r) }
        $vdf = Join-Path $r 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
                $lib = $m.Groups[1].Value -replace '\\\\', '\'
                if ((Test-Path (Join-Path $lib 'steamapps')) -and -not $libs.Contains($lib)) { $libs.Add($lib) }
            }
        }
    }
    return $libs
}

if (-not $GameDir) {
    foreach ($lib in Get-SteamLibraries) {
        $cand = Join-Path $lib 'steamapps\common\Big Ambitions'
        if (Test-Path (Join-Path $cand 'Big Ambitions_Data\Managed')) { $GameDir = $cand; break }
    }
}
if (-not $GameDir -or -not (Test-Path (Join-Path $GameDir 'Big Ambitions_Data\Managed'))) {
    throw 'Big Ambitions nicht gefunden. Mit -GameDir "X:\...\steamapps\common\Big Ambitions" angeben.'
}
# No trailing backslash: '...\"' would escape the closing quote of a path with spaces. The csproj adds the slash.
$GameDir = $GameDir.TrimEnd('\')
Say "Spiel: $GameDir"

$gameData = Join-Path $env:USERPROFILE 'AppData\LocalLow\Hovgaard Games\Big Ambitions'
$mods = Join-Path $gameData 'ModsLocal'
Say "Mods:  $mods"

# ── Old separate Telemetry Bridge: move it out of ModsLocal ────────────────────────────────────────────────
if (Test-Path $mods) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backup = Join-Path $gameData 'ModsLocal-Backup'
    $found = $false
    foreach ($d in Get-ChildItem -LiteralPath $mods -Directory) {
        if ($d.Name -eq 'GoingPublicServer') { continue }
        # the mod's folder before the rename to GoingPublicServer: two copies would be refused as a duplicate install
        $isBridge = ($d.Name -eq 'BigAmbitionsMP') -or ($d.Name -like '*Telemetry*Bridge*') -or
                    (Get-ChildItem -LiteralPath $d.FullName -Recurse -Depth 2 -Filter '*Telemetry*Bridge*.dll' -ErrorAction SilentlyContinue | Select-Object -First 1)
        if (-not $isBridge) { continue }
        $found = $true
        $target = Join-Path $backup ("{0}-{1}" -f $d.Name, $stamp)
        Say "Alte Mod-Kopie (BigAmbitionsMP / Telemetry Bridge) gefunden: $($d.FullName)"
        if ($DryRun) { Write-Host "   (dry-run) verschieben nach $target"; continue }
        New-Item -ItemType Directory -Force -Path $backup | Out-Null
        Move-Item -LiteralPath $d.FullName -Destination $target
        Say "  -> verschoben nach $target (zum Wiederherstellen zurück nach ModsLocal schieben)"
    }
    if (-not $found) { Say 'Keine separate Telemetry Bridge in ModsLocal gefunden - gut.' }
}

if ($NoBuild) { return }

# ── Build + install (the csproj copies the finished mod into ModsLocal\GoingPublicServer) ─────────────────────
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK fehlt. Installieren mit:  winget install Microsoft.DotNet.SDK.8'
}
$deployDir = Join-Path $mods 'GoingPublicServer'
$deploy = $deployDir + '/'   # forward slash: MSBuild needs the trailing separator, a backslash would eat the quote
Say "Baue Going Public Server ($config) ..."
dotnet build (Join-Path $root 'BigAmbitionsMP.csproj') -c $config -nologo "-p:GameDir=$GameDir" "-p:ModDeployDir=$deploy"
if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen - Ausgabe oben prüfen.' }
if (-not (Test-Path (Join-Path $deployDir 'BigAmbitionsMP.dll'))) { throw "Build lief durch, aber $deployDir\BigAmbitionsMP.dll fehlt." }
Say "Installiert: $deployDir"
Get-ChildItem -LiteralPath $deployDir -Recurse | Select-Object FullName, Length | Format-Table -AutoSize
Write-Host ''
Write-Host 'Fertig. Jetzt:'
Write-Host '  1. Big Ambitions starten, im Mod-Menü "Going Public" aktiv lassen.'
Write-Host "  2. Im Log nach 'Going Public Server' suchen: $gameData\Player.log"
