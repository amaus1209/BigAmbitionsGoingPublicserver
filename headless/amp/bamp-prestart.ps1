# PRESTART-WIN-1 (user 2026-11-12: "erstelle eine vorlage für windows"): the Windows side of bamp-prestart.sh for the
# AMP template. On Windows AMP starts "1331550\Big Ambitions.exe" itself, without Proton; the game loads its mods from
# <root>\mods (-bamp-mods-dir, the preloader's ModsLocalPath) and runs without a Steam client (-bamp-nosteam), as the
# stand-alone Windows server does. Windows PowerShell 5.1, no extra programs; Python only where it is installed.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File bamp-prestart.ps1 <step> <root folder> [<bamp-server.json>]
#     leftovers     end a game of this instance from the last run that is still there (it keeps the port and the save)
#     settings      bamp-server.json with its first values, then every setting of the template with its default
#     localize      the settings page in the server's language (needs Python; without it the page stays English)
#     key           the key players type into "Server key" (SERVER-KEY-1)
#     mods          Going Public Server and the extra mods into <root>\mods, the list of extra mods for the players
#     all           leftovers, settings, key, mods
#     steam <name>  update stage: the game through SteamCMD with its remembered login (STEAM-LOGIN-1), not part of "all"
param([string]$Step = "all", [string]$Root = "", [string]$Conf = "", [string]$Lang = "")
$ErrorActionPreference = "Continue"
# PRESTART-WIN-2 (first run on Windows 10, 2026-11-13): AMP reads a stage's output in the console's code page (OEM, 850
# on a German Windows) - UTF-8 made "Schl├╝ssel" of "Schlüssel". The output stays in the console's own code page.

function Say([string]$t) { Write-Output "[BAMP-Start] $t" }

# PRESTART-WIN-2: Windows PowerShell 5.1's ConvertFrom-Json refuses an empty key - the template has one ("" in the
# EnumValues of "DedicatedSession": the world played last) and the step "settings" stopped there. JavaScriptSerializer
# (.NET Framework) reads it: objects come back as dictionaries, lists as arrays.
function Read-JsonFile([string]$path) {
    Add-Type -AssemblyName System.Web.Extensions
    $js = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $js.MaxJsonLength = 67108864
    return $js.DeserializeObject([IO.File]::ReadAllText($path))
}

function Field($d, [string]$k) {
    if ($d -is [Collections.IDictionary] -and $d.ContainsKey($k)) { return $d[$k] }
    return $null
}

if ($Root.Length -eq 0) { Say "bamp-prestart.ps1: kein Server-Ordner angegeben - nichts zu tun."; exit 0 }
$Root = [IO.Path]::GetFullPath($Root.TrimEnd('\', '/', '.').TrimEnd('\', '/'))
if (-not (Test-Path -LiteralPath $Root -PathType Container)) { Say "bamp-prestart.ps1: '$Root' fehlt - nichts zu tun."; exit 0 }
if ($Conf.Length -eq 0) { $Conf = Join-Path $Root "1331550\bamp-server.json" }
$Tools = Join-Path $Root "bamp-tools"
$Mods = Join-Path $Root "mods"
$Utf8 = New-Object System.Text.UTF8Encoding $false

function Read-Conf {
    if (-not (Test-Path -LiteralPath $Conf)) { return $null }
    try { return ([IO.File]::ReadAllText($Conf) | ConvertFrom-Json) } catch { Say "bamp-server.json ist nicht lesbar: $($_.Exception.Message)"; return $null }
}

function Write-Conf($c) {
    [IO.File]::WriteAllText($Conf, ($c | ConvertTo-Json -Depth 20), $Utf8)
}

function Clean-Key([string]$s) {
    if ($null -eq $s) { return "" }
    $k = -join ($s.ToCharArray() | Where-Object { ($_ -match '[A-Za-z0-9_-]') })
    if ($k.Length -gt 64) { $k = $k.Substring(0, 64) }
    return $k
}

function Conf-Key {
    $c = Read-Conf
    if ($null -eq $c -or $null -eq $c.TelemetryKey) { return "" }
    return Clean-Key ([string]$c.TelemetryKey)
}

# The game of the last run, if AMP's stop did not end it: only this instance's (its folder is in the command line).
function Step-Leftovers {
    $procs = @(Get-CimInstance Win32_Process -Filter "Name='Big Ambitions.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($Root, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
    if ($procs.Count -eq 0) { return }
    Say ("Ein Spiel vom letzten Lauf läuft noch (Prozess " + (($procs | ForEach-Object { $_.ProcessId }) -join ", ") + ") - es wird beendet.")
    foreach ($p in $procs) { Stop-Process -Id $p.ProcessId -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 5
    foreach ($p in $procs) { Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue }
}

# The same first values as the Linux update stage "Default server settings", then SETTINGS-SEED-1: every setting of the
# template gets its key with the default (AMP writes its values only into keys that are already in the file).
function Step-Settings {
    foreach ($d in @("headless", "bamp-mod", "telemetry-data")) { New-Item -ItemType Directory -Force -Path (Join-Path $Root $d) | Out-Null }
    if (-not (Test-Path -LiteralPath $Conf)) {
        $first = '{"ServerLanguage": "en", "Headless": true, "DedicatedHost": true, "DedicatedSession": "", "DedicatedNewGame": "", ' +
                 '"DedicatedLoadSave": "", "PlayerId": "Server", "HostPort": 7777, "DedicatedIdleGraceSeconds": 0, "DedicatedAfkMinutes": 5, ' +
                 '"DedicatedPauseInMenu": true, "DedicatedRequireKey": true, "DedicatedAutoAccept": true, "AutosaveMinutes": 5, ' +
                 '"AutosaveSlots": "3", "MaxPlayers": "8", "PlayerWebUrl": "", "HeadlessFps": 200, "HeadlessIdleFps": 5, "TelemetryKey": ""}'
        [IO.File]::WriteAllText($Conf, $first + "`n", $Utf8)
        Say "bamp-server.json angelegt."
    }
    $manifest = Join-Path $Tools "bigambitionsconfig.json"
    if (-not (Test-Path -LiteralPath $manifest)) { return }
    $c = Read-Conf
    if ($null -eq $c) { return }
    $have = @{}
    foreach ($p in $c.PSObject.Properties) { $have[$p.Name] = $true }
    $added = @()
    $settings = @()
    try { $settings = @(Read-JsonFile $manifest) } catch { Say "bigambitionsconfig.json ist nicht lesbar: $($_.Exception.Message)"; return }
    foreach ($s in $settings) {
        $field = [string](Field $s "ParamFieldName")
        if (-not $field.StartsWith('$.')) { continue }
        $key = $field.Substring(2)
        if ($have.ContainsKey($key)) { continue }
        $v = Field $s "DefaultValue"
        $type = [string](Field $s "InputType")
        if ($type -eq "checkbox") { $v = ([string]$v).ToLower() -eq "true" }
        elseif ($type -eq "number") {
            $i = 0; $f = 0.0
            if ([int]::TryParse([string]$v, [ref]$i)) { $v = $i }
            elseif ([double]::TryParse([string]$v, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$f)) { $v = $f }
        }
        elseif ($null -eq $v) { $v = "" }
        $c | Add-Member -NotePropertyName $key -NotePropertyValue $v
        $have[$key] = $true
        $added += $key
    }
    if ($added.Count -gt 0) {
        Write-Conf $c
        Write-Output ("[BAMP] bamp-server.json ergänzt um: " + ($added -join ", ") + " (AMP trägt seine Werte beim nächsten Start ein).")
    }
}

# A real Python (not the Microsoft Store placeholder in WindowsApps), or "".
function Find-Python {
    foreach ($n in @("py", "python3", "python")) {
        $cmd = Get-Command $n -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($cmd -and $cmd.Source -notmatch '\\WindowsApps\\') { return $cmd.Source }
    }
    return ""
}

function Step-Localize {
    $tool = Join-Path $Tools "localize-template.py"
    if (-not (Test-Path -LiteralPath $tool)) { return }
    $py = Find-Python
    if ($py.Length -eq 0) { Say "Einstellungen in der Server-Sprache: Python fehlt auf diesem Windows - die Einstellungsseite bleibt englisch."; return }
    $l = $Lang
    if ($l.Length -eq 0) { $c = Read-Conf; if ($c -and $c.ServerLanguage) { $l = [string]$c.ServerLanguage } else { $l = "en" } }
    $manifest = [IO.Path]::GetFullPath((Join-Path $Root "..\configmanifest.json"))
    & $py -I $tool $l --manifest $manifest
}

# SERVER-KEY-1: the AMP setting wins; otherwise telemetry-data\key.txt, made once (12 hex characters).
function Step-Key {
    New-Item -ItemType Directory -Force -Path (Join-Path $Root "telemetry-data") | Out-Null
    if ((Conf-Key).Length -gt 0) { return }
    $kfile = Join-Path $Root "telemetry-data\key.txt"
    $key = ""
    if (Test-Path -LiteralPath $kfile) { $key = Clean-Key ([IO.File]::ReadAllText($kfile)) }
    if ($key.Length -eq 0) {
        $b = New-Object byte[] 6
        [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
        $key = -join ($b | ForEach-Object { $_.ToString("x2") })
        [IO.File]::WriteAllText($kfile, $key + "`n", $Utf8)
        Write-Output "[Server] Neuer Server-Schlüssel erstellt."
    }
}

function Same-Folder([string]$a, [string]$b) {
    if (-not (Test-Path -LiteralPath $b)) { return $false }
    $fa = @(Get-ChildItem -LiteralPath $a -Recurse -File -ErrorAction SilentlyContinue)
    $fb = @(Get-ChildItem -LiteralPath $b -Recurse -File -ErrorAction SilentlyContinue)
    if ($fa.Count -ne $fb.Count) { return $false }
    foreach ($f in $fa) {
        $rel = $f.FullName.Substring($a.Length).TrimStart('\')
        $g = Join-Path $b $rel
        if (-not (Test-Path -LiteralPath $g)) { return $false }
        if ((Get-Item -LiteralPath $g).Length -ne $f.Length) { return $false }
        if ((Get-FileHash -LiteralPath $g).Hash -ne (Get-FileHash -LiteralPath $f.FullName).Hash) { return $false }
    }
    return $true
}

function Copy-Mod([string]$src, [string]$name) {
    $dst = Join-Path $Mods $name
    if (Same-Folder $src $dst) { return $false }
    if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
    Copy-Item -LiteralPath $src -Destination $dst -Recurse -Force
    return $true
}

# The server's mods folder (the game reads it through -bamp-mods-dir): Going Public Server from bamp-mod\, extra mods
# from bamp-mods-extra\ (AMP's Steam Workshop store); a Workshop item that left "Steam Workshop Items" is removed
# (SERVER-MODS-2). Then the LIST of extra mods for the players (SERVER-MODS-1, never the files - SEC-MODS-1).
function Step-Mods {
    New-Item -ItemType Directory -Force -Path $Mods, (Join-Path $Root "bamp-mods-extra") | Out-Null
    $gps = Join-Path $Root "bamp-mod\GoingPublicServer"
    if (Test-Path -LiteralPath (Join-Path $gps "BigAmbitionsMP.dll")) {
        if (Copy-Mod $gps "GoingPublicServer") { Say "Going Public Server aus bamp-mod\ in mods\ kopiert." }
    } else { Say "bamp-mod\GoingPublicServer fehlt - in AMP 'Update' ausführen." }
    foreach ($d in @(Get-ChildItem -LiteralPath (Join-Path $Root "bamp-mods-extra") -Directory -ErrorAction SilentlyContinue)) {
        if ($d.Name -eq "GoingPublicServer") { continue }
        if ((Test-Path -LiteralPath (Join-Path $d.FullName "Layout.json")) -and -not (Get-ChildItem -LiteralPath $d.FullName -Filter *.dll)) {
            Say "'$($d.Name)' ist ein Blueprint - bleibt beim Spieler, nicht installiert."; continue
        }
        if (Copy-Mod $d.FullName $d.Name) { Say "Mod '$($d.Name)' aus bamp-mods-extra\ installiert." }
    }
    $kvp = [IO.Path]::GetFullPath((Join-Path $Root "..\steamcmdplugin.kvp"))
    if (Test-Path -LiteralPath $kvp) {
        $line = Get-Content -LiteralPath $kvp -Encoding UTF8 | Where-Object { $_.StartsWith("SteamWorkshop.WorkshopItemIDs=") } | Select-Object -First 1
        if ($line) {
            $wanted = @()
            try { $wanted = @(($line.Substring($line.IndexOf('=') + 1) | ConvertFrom-Json) | ForEach-Object { ([string]$_).Trim() }) } catch { $line = $null }
            if ($line) {
                foreach ($dir in @((Join-Path $Root "bamp-mods-extra"), $Mods)) {
                    foreach ($d in @(Get-ChildItem -LiteralPath $dir -Directory -ErrorAction SilentlyContinue)) {
                        if ($d.Name -notmatch '^[0-9]+$' -or $wanted -contains $d.Name) { continue }
                        Remove-Item -LiteralPath $d.FullName -Recurse -Force
                        Say "Workshop-Mod '$($d.Name)' ist nicht mehr unter `"Steam Workshop Items`" - entfernt ($(Split-Path $dir -Leaf)\)."
                    }
                }
            }
        }
    }
    $key = Conf-Key
    $kfile = Join-Path $Root "telemetry-data\key.txt"
    if ($key.Length -eq 0 -and (Test-Path -LiteralPath $kfile)) { $key = Clean-Key ([IO.File]::ReadAllText($kfile)) }
    if ($key.Length -eq 0) { return }
    # the folder is named after SHA-256(key), never the key itself (DedicatedHost.KeyFolder)
    $sha = [Security.Cryptography.SHA256]::Create().ComputeHash($Utf8.GetBytes($key.ToLowerInvariant()))
    $kdir = Join-Path $Root ("telemetry-data\web\" + (-join ($sha | ForEach-Object { $_.ToString("x2") })).Substring(0, 16))
    $out = Join-Path $kdir "mods"
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    $ranking = Join-Path $kdir "ranking.json"
    if (-not (Test-Path -LiteralPath $ranking)) {
        [IO.File]::WriteAllText($ranking, '{"updatedUnix": ' + [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() + ', "players": []}' + "`n", $Utf8)
    }
    $entries = @()
    foreach ($d in @(Get-ChildItem -LiteralPath $Mods -Directory | Sort-Object Name)) {
        if ($d.Name -eq "GoingPublicServer") { continue }
        if ((Test-Path -LiteralPath (Join-Path $d.FullName "Layout.json")) -and -not (Get-ChildItem -LiteralPath $d.FullName -Filter *.dll)) { continue }
        $entries += ,@{ folder = $d.Name; workshop = ($d.Name -match '^[0-9]+$') }
    }
    Get-ChildItem -LiteralPath $out -File | Where-Object { $_.Name -ne "mods.json" } | Remove-Item -Force
    $json = '{"mods": [' + (($entries | ForEach-Object { '{"folder": ' + ($_.folder | ConvertTo-Json) + ', "workshop": ' + $_.workshop.ToString().ToLower() + '}' }) -join ', ') + ']}'
    [IO.File]::WriteAllText((Join-Path $out "mods.json"), $json + "`n", $Utf8)
    if ($entries.Count -gt 0) { Say ("$($entries.Count) Server-Mod(s) in der Liste für Spieler: " + (($entries | ForEach-Object { $_.folder }) -join ", ")) }
}

# STEAM-LOGIN-1 (user 2026-11-13: "möchte die zugangsdaten in der instanz unter einstellungen bigambitions eintragen", then
# "Nur Kontoname"): the update stage "SteamCMD Download (remembered login)" when the AMP setting SteamUpdateLogin is
# "remembered" - the game through SteamCMD with the login it remembered on this server after one login through AMP's
# window (Steam Guard is asked only then). Only the account name (4th argument, AMP setting SteamUpdateUser), no password;
# SteamCMD's token lies in <root>\config, where AMP's own SteamCMD stage keeps it. +@NoPromptForPassword and a closed
# stdin: a login SteamCMD no longer remembers fails at once with a message instead of waiting for a password at night;
# a run that hangs ends after two hours. The account name never reaches AMP's console or log (shown as <Konto>).
function Step-Steam([string]$user) {
    $user = ([string]$user).Trim()
    if ($user.Length -eq 0) { Say "Steam-Anmeldung 'Gemerkte Anmeldung' ist gewählt, aber kein Steam-Kontoname eingetragen (Einstellungen > Big Ambitions > Steam-Updates)."; exit 1 }
    if ($user -notmatch '^[A-Za-z0-9_.@-]{1,64}$') { Say "Der Steam-Kontoname enthält Zeichen, die hier nicht gehen (erlaubt: Buchstaben, Ziffern, _ . @ -)."; exit 1 }
    $exe = Join-Path $Root "steamcmd.exe"
    if (-not (Test-Path -LiteralPath $exe)) { Say "SteamCMD fehlt in dieser Instanz - einmal mit 'Steam-Anmeldung: Login-Fenster' aktualisieren."; exit 1 }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exe
    $psi.Arguments = '+@ShutdownOnFailedCommand 1 +@NoPromptForPassword 1 +@sSteamCmdForcePlatformType windows +force_install_dir "' +
                     (Join-Path $Root "1331550") + '" +login ' + $user + ' +app_update 1331550 +quit'
    $psi.WorkingDirectory = $Root
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    Say "Spiel-Update über SteamCMD mit der gemerkten Anmeldung ..."
    $p = [Diagnostics.Process]::Start($psi)
    $p.StandardInput.Close()   # no password prompt can wait for an answer
    $null = $p.StandardError.ReadToEndAsync()
    $ok = $false; $why = ""
    $deadline = (Get-Date).AddHours(2)
    while ($true) {
        $t = $p.StandardOutput.ReadLineAsync()
        while (-not $t.Wait(1000)) { if ((Get-Date) -gt $deadline) { break } }
        if (-not $t.IsCompleted) { try { $p.Kill() } catch { }; $why = "nach zwei Stunden abgebrochen"; break }
        $line = $t.Result
        if ($null -eq $line) { break }
        if ($line -match "Success! App '1331550' (fully installed|already up to date)") { $ok = $true }
        if ($line -match '(?i)Cached credentials not found|password|Login Failure|Steam Guard|two-factor|Rate Limit|Expired|FAILED') { $why = $line.Trim() }
        if ($line.Trim().Length -gt 0) { Write-Output ("[SteamCMD] " + $line.Replace($user, '<Konto>')) }
    }
    $null = $p.WaitForExit(30000)
    if ($ok) { Say "Spiel-Update mit der gemerkten Steam-Anmeldung fertig."; return }
    $why = $why.Replace($user, '<Konto>')
    Say ("Spiel-Update mit der gemerkten Steam-Anmeldung ging nicht" + $(if ($why.Length -gt 0) { " ($why)" } else { "" }) +
         ". Steam-Anmeldung einmal auf 'Login-Fenster' stellen, Update drücken und anmelden, danach wieder 'Gemerkte Anmeldung'.")
    exit 1
}

switch ($Step) {
    "steam" { Step-Steam $Lang }   # STEAM-LOGIN-1: the 4th argument is the Steam account name here
    "leftovers" { Step-Leftovers }
    "settings" { Step-Settings }
    "localize" { Step-Localize }
    "key" { Step-Key }
    "mods" { Step-Mods }
    "all" { Step-Leftovers; Step-Settings; Step-Key; Step-Mods }
    default { Say "bamp-prestart.ps1: unbekannter Schritt '$Step'." }
}
exit 0
