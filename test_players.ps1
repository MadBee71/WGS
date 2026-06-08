# WGS v1.0.4 - Pelaajanhallinta testiskripti
# Suorita: .\test_players.ps1

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$pass = 0; $fail = 0

function OK($msg)  { Write-Host "  [OK]  $msg" -ForegroundColor Green;  $script:pass++ }
function FAIL($msg){ Write-Host "  [ERR] $msg" -ForegroundColor Red;    $script:fail++ }
function HEAD($msg){ Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

# ---------------------------------------------------------------------------
HEAD "1. Source Engine status-parseri"

# Tyypillinen CS2/TF2 status-rivisto
$sourceStatus = @"
hostname: My CS2 Server
version : 1.38.7.9
map     : de_dust2
# userid name                uniqueid            connected ping loss state
#      2 "PlayerOne"         STEAM_0:1:12345678  01:23:45   42    0 active
#      3 "xX_Sniper_Xx"      [U:1:87654321]      00:05:12   88    2 active
#      4 "Bot_Easy"          BOT                 00:00:01    0    0 active
"@

# Simuloi Source-parseri
$sourcePattern = '^#\s+\d+\s+"(?<name>[^"]+)"\s+(?<steam>\S+)\s+(?<time>\d+:\d+:\d+)\s+(?<ping>\d+)'
$sourcePlayers = [System.Collections.Generic.List[object]]::new()
foreach ($line in $sourceStatus -split "`n") {
    if ($line -match $sourcePattern) {
        $timeParts = $Matches['time'] -split ':'
        $secs = [int]$timeParts[0]*3600 + [int]$timeParts[1]*60 + [int]$timeParts[2]
        $sourcePlayers.Add([PSCustomObject]@{
            Name    = $Matches['name']
            SteamId = $Matches['steam']
            Ping    = [int]$Matches['ping']
            Seconds = $secs
        })
    }
}

if ($sourcePlayers.Count -eq 3)          { OK "Source: 3 pelaajaa parsittu" }
else                                      { FAIL "Source: odotettiin 3, saatiin $($sourcePlayers.Count)" }

if ($sourcePlayers[0].Name -eq "PlayerOne") { OK "Source: nimi 'PlayerOne' oikein" }
else                                         { FAIL "Source: nimi vaara: $($sourcePlayers[0].Name)" }

if ($sourcePlayers[0].SteamId -eq "STEAM_0:1:12345678") { OK "Source: SteamID parsittu" }
else                                                      { FAIL "Source: SteamID vaara: $($sourcePlayers[0].SteamId)" }

if ($sourcePlayers[0].Ping -eq 42)       { OK "Source: ping 42 oikein" }
else                                      { FAIL "Source: ping vaara: $($sourcePlayers[0].Ping)" }

$expectedSecs = 1*3600 + 23*60 + 45
if ($sourcePlayers[0].Seconds -eq $expectedSecs) { OK "Source: peliaika 01:23:45 = $expectedSecs s" }
else                                              { FAIL "Source: peliaika vaara: $($sourcePlayers[0].Seconds)" }

if ($sourcePlayers[1].SteamId -eq "[U:1:87654321]") { OK "Source: U:1: format parsittu" }
else                                                  { FAIL "Source: U:1: format vaara" }

# ---------------------------------------------------------------------------
HEAD "2. Rust playerlist-parseri"

$rustList = '[{"DisplayName":"RustPlayer1","SteamID":76561198012345678,"Ping":55,"ConnectedSeconds":3600},{"DisplayName":"Survivor2","SteamID":76561198087654321,"Ping":120,"ConnectedSeconds":120}]'

$rustPattern = '"DisplayName"\s*:\s*"(?<name>[^"]*)"\s*,\s*"SteamID"\s*:\s*(?<steam>\d+)\s*,\s*"Ping"\s*:\s*(?<ping>\d+)\s*,\s*"ConnectedSeconds"\s*:\s*(?<time>\d+)'
$rustPlayers = [System.Collections.Generic.List[object]]::new()
foreach ($m in [regex]::Matches($rustList, $rustPattern)) {
    $rustPlayers.Add([PSCustomObject]@{
        Name    = $m.Groups['name'].Value
        SteamId = $m.Groups['steam'].Value
        Ping    = [int]$m.Groups['ping'].Value
        Seconds = [int]$m.Groups['time'].Value
    })
}

if ($rustPlayers.Count -eq 2)               { OK "Rust: 2 pelaajaa parsittu" }
else                                          { FAIL "Rust: odotettiin 2, saatiin $($rustPlayers.Count)" }
if ($rustPlayers[0].Name -eq "RustPlayer1") { OK "Rust: nimi oikein" }
else                                          { FAIL "Rust: nimi vaara" }
if ($rustPlayers[0].Seconds -eq 3600)        { OK "Rust: ConnectedSeconds=3600" }
else                                          { FAIL "Rust: sekunnit vaara" }

# ---------------------------------------------------------------------------
HEAD "3. Minecraft list-parseri"

$mcList = "There are 2 of a max of 20 players online: Steve, Alex"
$mcMatch = [regex]::Match($mcList, "players online:\s*(.+)$")
$mcNames = @($mcMatch.Groups[1].Value -split ',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }

if ($mcNames.Count -eq 2)      { OK "Minecraft: 2 pelaajaa parsittu" }
else                            { FAIL "Minecraft: maara vaara: $($mcNames.Count)" }
if ($mcNames -contains "Steve") { OK "Minecraft: Steve loytyy" }
else                             { FAIL "Minecraft: Steve puuttuu" }
if ($mcNames -contains "Alex")  { OK "Minecraft: Alex loytyy" }
else                             { FAIL "Minecraft: Alex puuttuu" }

# ---------------------------------------------------------------------------
HEAD "4. ARK ListPlayers-parseri"

$arkList = @"
0. ArkPlayer1, 76561198111111111
1. Survivor_X, 76561198222222222
"@

$arkPattern = '^\d+\.\s+(?<name>.+?),\s+(?<steam>\d+)'
$arkPlayers = [System.Collections.Generic.List[object]]::new()
foreach ($line in $arkList -split "`n") {
    if ($line -match $arkPattern) {
        $arkPlayers.Add([PSCustomObject]@{ Name=$Matches['name'].Trim(); SteamId=$Matches['steam'] })
    }
}

if ($arkPlayers.Count -eq 2)                   { OK "ARK: 2 pelaajaa parsittu" }
else                                            { FAIL "ARK: maara vaara" }
if ($arkPlayers[0].SteamId -eq "76561198111111111") { OK "ARK: SteamID oikein" }
else                                                  { FAIL "ARK: SteamID vaara" }

# ---------------------------------------------------------------------------
HEAD "5. Kick/Ban komennot perusteluineen"

# Source
function SourceKick($p, $r) { return "kickid `"$p`" `"$r`"" }
function SourceBan($p, $r)  { return "banid 0 `"$p`" kick; say `"$r`"" }
function RustKick($p, $r)   { return "kick `"$p`" `"$r`"" }
function RustBan($p, $r)    { return "ban `"$p`" `"$r`"" }
function McBan($p, $r)      { return "ban $p $r" }

$k = SourceKick "76561198012345678" "Cheating"
if ($k -eq 'kickid "76561198012345678" "Cheating"') { OK "Source kick perustelu oikein" }
else                                                  { FAIL "Source kick: $k" }

$b = SourceBan "76561198012345678" "Hacking"
if ($b -match "banid 0.*Hacking") { OK "Source ban perustelu oikein" }
else                               { FAIL "Source ban: $b" }

$rk = RustKick "76561198012345678" "AFK"
if ($rk -eq 'kick "76561198012345678" "AFK"') { OK "Rust kick perustelu oikein" }
else                                            { FAIL "Rust kick: $rk" }

$mcb = McBan "Steve" "Griefing"
if ($mcb -eq "ban Steve Griefing") { OK "Minecraft ban perustelu oikein" }
else                                { FAIL "Minecraft ban: $mcb" }

# ---------------------------------------------------------------------------
HEAD "6. Session logging SQLite-logiikka"

# Simuloi DB-operaatiot muistissa (SQLite ei ole PowerShell GAC:ssa)
$sessions = [System.Collections.Generic.List[hashtable]]::new()

function RecordJoin($serverId, $playerName, $steamId) {
    $script:sessions.Add(@{
        ServerId   = $serverId
        PlayerName = $playerName
        SteamId    = $steamId
        JoinTime   = [datetime]::UtcNow
        LeaveTime  = $null
    })
}

function RecordLeave($serverId, $steamId) {
    $last = $script:sessions | Where-Object { $_.ServerId -eq $serverId -and $_.SteamId -eq $steamId -and $null -eq $_.LeaveTime } | Select-Object -Last 1
    if ($last) { $last.LeaveTime = [datetime]::UtcNow }
}

RecordJoin "srv1" "Alice" "111"
RecordJoin "srv1" "Bob"   "222"
Start-Sleep -Milliseconds 50
RecordLeave "srv1" "111"

$s1 = @($sessions | Where-Object { $_.ServerId -eq "srv1" })
if ($s1.Count -eq 2)                              { OK "DB: 2 sessiota kirjattu" }
else                                               { FAIL "DB: sessioiden maara vaara: $($s1.Count)" }

$alice = $s1 | Where-Object { $_.SteamId -eq "111" }
if ($null -ne $alice.LeaveTime)                   { OK "DB: Alice leave_time kirjattu" }
else                                               { FAIL "DB: Alice leave_time puuttuu" }

$bob = $s1 | Where-Object { $_.SteamId -eq "222" }
if ($null -eq $bob.LeaveTime)                     { OK "DB: Bob on edelleen paikalla (leave_time null)" }
else                                               { FAIL "DB: Bob leave_time pitaisi olla null" }

$duration = ($alice.LeaveTime - $alice.JoinTime).TotalMilliseconds
if ($duration -gt 0)                              { OK "DB: Kesto laskettu ($([math]::Round($duration)) ms)" }
else                                               { FAIL "DB: Kesto 0 tai negatiivinen" }

# ---------------------------------------------------------------------------
HEAD "7. Auto-refresh logiikka (diff-detectio)"

$prev = @(
    [PSCustomObject]@{ Name="Alice"; SteamId="111" }
    [PSCustomObject]@{ Name="Bob";   SteamId="222" }
)
$curr = @(
    [PSCustomObject]@{ Name="Alice"; SteamId="111" }
    [PSCustomObject]@{ Name="Charlie"; SteamId="333" }
)

$prevKeys = $prev | ForEach-Object { if ($_.SteamId) { $_.SteamId } else { $_.Name } }
$currKeys = $curr | ForEach-Object { if ($_.SteamId) { $_.SteamId } else { $_.Name } }

$joined  = @($curr | Where-Object { $key = if ($_.SteamId) { $_.SteamId } else { $_.Name }; $prevKeys -notcontains $key })
$left    = @($prev | Where-Object { $key = if ($_.SteamId) { $_.SteamId } else { $_.Name }; $currKeys -notcontains $key })

if ($joined.Count -eq 1 -and $joined[0].Name -eq "Charlie") { OK "Diff: Charlie havaittu liittyneeksi" }
else                                                          { FAIL "Diff: liittyjien tunnistus epaonnistui" }

if ($left.Count -eq 1 -and $left[0].Name -eq "Bob")         { OK "Diff: Bob havaittu poistuneeksi" }
else                                                          { FAIL "Diff: poistujien tunnistus epaonnistui" }

# ---------------------------------------------------------------------------
HEAD "8. OnlinePlayer-malli"

$p1 = [PSCustomObject]@{ ConnectedSeconds=45;    SteamId="76561198012345678" }
$p2 = [PSCustomObject]@{ ConnectedSeconds=3720;  SteamId="76561198012345678" }
$p3 = [PSCustomObject]@{ ConnectedSeconds=7265;  SteamId="" }

function ConnText($secs) {
    if ($secs -lt 60)   { return "${secs}s" }
    if ($secs -lt 3600) { return "$([int]($secs/60))m $($secs % 60)s" }
    return "$([int]($secs/3600))h $([int](($secs % 3600)/60))m"
}
function SteamShort($id) {
    if ($id.Length -gt 10) { return "..." + $id.Substring($id.Length-8) }
    return $id
}

if ((ConnText $p1.ConnectedSeconds) -eq "45s")      { OK "ConnectedText: 45s oikein" }
else                                                  { FAIL "ConnectedText: $(ConnText $p1.ConnectedSeconds)" }

if ((ConnText $p2.ConnectedSeconds) -eq "1h 2m")    { OK "ConnectedText: 1h 2m oikein" }
else                                                  { FAIL "ConnectedText: $(ConnText $p2.ConnectedSeconds)" }

if ((ConnText $p3.ConnectedSeconds) -eq "2h 1m")    { OK "ConnectedText: 2h 1m oikein" }
else                                                  { FAIL "ConnectedText: $(ConnText $p3.ConnectedSeconds)" }

$short = SteamShort $p1.SteamId
if ($short -eq "...12345678")                        { OK "SteamIdShort oikein: $short" }
else                                                  { FAIL "SteamIdShort vaara: $short" }

Write-Host ""
Write-Host "---------------------------------------------" -ForegroundColor DarkGray
if ($fail -eq 0) {
    Write-Host "  KAIKKI $pass TESTIA LAPAISTY" -ForegroundColor Green
} else {
    Write-Host "  TULOKSET: $pass OK, $fail EPAONNISTUI" -ForegroundColor Red
}
Write-Host "---------------------------------------------" -ForegroundColor DarkGray
Write-Host ""

if ($fail -gt 0) { exit 1 }
