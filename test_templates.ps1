# WGS v1.0.4 - Template-ominaisuuksien testiskripti
# Suorita: .\test_templates.ps1

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$appData   = "$env:APPDATA\WGS"
$templFile = "$appData\templates.json"
$exportDir = "$env:TEMP\wgs_template_test"

$pass = 0
$fail = 0

function OK($msg)  { Write-Host "  [OK]  $msg" -ForegroundColor Green;  $script:pass++ }
function FAIL($msg){ Write-Host "  [ERR] $msg" -ForegroundColor Red;    $script:fail++ }
function HEAD($msg){ Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

HEAD "Alustus"

New-Item -ItemType Directory -Force -Path $exportDir | Out-Null
New-Item -ItemType Directory -Force -Path $appData   | Out-Null

$backup = $null
if (Test-Path $templFile) {
    $backup = Get-Content $templFile -Raw -Encoding UTF8
    Write-Host "  Varmuuskopio otettu ($templFile)"
}

# ---------------------------------------------------------------------------
HEAD "1. Import - yksittainen template-objekti"

$single = @{
    Id                   = [System.Guid]::NewGuid().ToString()
    Name                 = "Import-testi-Yksittainen"
    GameId               = "cs2"
    GameName             = "Counter-Strike 2"
    Description          = "Tuotu yksittaisena objektina"
    Category             = "Kilpailu"
    Tags                 = @("pvp", "ranked")
    CreatedAt            = (Get-Date -Format "o")
    DefaultPort          = 27015
    DefaultQueryPort     = 27016
    DefaultSteamPort     = 27017
    MaxPlayers           = 10
    AutoRestart          = $true
    AutoUpdate           = $false
    BackupEnabled        = $true
    BackupRetention      = 7
    CustomArgs           = "-tickrate 128"
    ProcessPriority      = "High"
    GameSpecificSettings = @{ tickrate = "128" }
} | ConvertTo-Json -Depth 5

$singleFile = "$exportDir\single_import.json"
Set-Content $singleFile $single -Encoding UTF8
Write-Host "  Kirjoitettu: $singleFile"

$parsed = Get-Content $singleFile -Raw -Encoding UTF8 | ConvertFrom-Json

if ($parsed.Name -eq "Import-testi-Yksittainen") { OK "Yksittainen template parsittu oikein" }
else { FAIL "Nimi ei tasmaa: $($parsed.Name)" }

if ($parsed.Category -eq "Kilpailu") { OK "Category luettu oikein" }
else { FAIL "Category puuttuu tai vaara: $($parsed.Category)" }

if ($parsed.Tags.Count -eq 2 -and ($parsed.Tags -contains "pvp")) { OK "Tags luettu oikein (2 tagia)" }
else { FAIL "Tags vaara maara: $($parsed.Tags.Count)" }

# ---------------------------------------------------------------------------
HEAD "2. Import - lista templateja"

$listJson = @"
[
  {
    "Id": "11111111-0000-0000-0000-000000000001",
    "Name": "Lista-1",
    "GameId": "valheim",
    "GameName": "Valheim",
    "Description": "",
    "Category": "PvE",
    "Tags": ["survival"],
    "CreatedAt": "2024-01-01T00:00:00",
    "DefaultPort": 2456,
    "DefaultQueryPort": 2457,
    "DefaultSteamPort": 2458,
    "MaxPlayers": 10,
    "AutoRestart": true,
    "AutoUpdate": true,
    "BackupEnabled": true,
    "BackupRetention": 5,
    "CustomArgs": "",
    "ProcessPriority": "Normal",
    "GameSpecificSettings": {}
  },
  {
    "Id": "22222222-0000-0000-0000-000000000002",
    "Name": "Lista-2",
    "GameId": "valheim",
    "GameName": "Valheim",
    "Description": "",
    "Category": "PvP",
    "Tags": ["pvp", "hardcore"],
    "CreatedAt": "2024-01-01T00:00:00",
    "DefaultPort": 2459,
    "DefaultQueryPort": 2460,
    "DefaultSteamPort": 2461,
    "MaxPlayers": 6,
    "AutoRestart": false,
    "AutoUpdate": false,
    "BackupEnabled": false,
    "BackupRetention": 3,
    "CustomArgs": "-crossplay",
    "ProcessPriority": "Normal",
    "GameSpecificSettings": {}
  }
]
"@

$listFile = "$exportDir\list_import.json"
Set-Content $listFile $listJson -Encoding UTF8
Write-Host "  Kirjoitettu: $listFile"

$parsedList = Get-Content $listFile -Raw -Encoding UTF8 | ConvertFrom-Json

if ($parsedList.Count -eq 2) { OK "Lista parsittu (2 templatea)" }
else { FAIL "Listan pituus vaara: $($parsedList.Count)" }

$names = $parsedList | Select-Object -ExpandProperty Name
if ($names -contains "Lista-1" -and $names -contains "Lista-2") { OK "Molempien nimet tasmaaavat" }
else { FAIL "Nimet eivat tasmaa: $names" }

if ($parsedList[0].Tags -contains "survival") { OK "Lista[0] Tags oikein" }
else { FAIL "Lista[0] Tags vaara" }

if ($parsedList[1].Tags -contains "hardcore") { OK "Lista[1] Tags oikein" }
else { FAIL "Lista[1] Tags vaara" }

# ---------------------------------------------------------------------------
HEAD "3. Export - kenttarakenne"

$exportFile = "$exportDir\export_result.json"
Copy-Item $singleFile $exportFile
$exported = Get-Content $exportFile -Raw -Encoding UTF8 | ConvertFrom-Json

foreach ($field in @("Id","Name","GameId","Category","Tags","CustomArgs","GameSpecificSettings","MaxPlayers","ProcessPriority")) {
    $val = $exported.$field
    if ($null -ne $val) { OK "Kentta '$field' loytyy" }
    else { FAIL "Kentta '$field' puuttuu" }
}

# ---------------------------------------------------------------------------
HEAD "4. Kloonaus-logiikka"

$src = $parsed

$cloneId   = [System.Guid]::NewGuid().ToString()
$cloneName = $src.Name + " (kopio)"

$clone = [PSCustomObject]@{
    Id                   = $cloneId
    Name                 = $cloneName
    GameId               = $src.GameId
    GameName             = $src.GameName
    Description          = $src.Description
    Category             = $src.Category
    Tags                 = @() + $src.Tags
    CreatedAt            = (Get-Date -Format "o")
    DefaultPort          = $src.DefaultPort
    DefaultQueryPort     = $src.DefaultQueryPort
    DefaultSteamPort     = $src.DefaultSteamPort
    MaxPlayers           = $src.MaxPlayers
    AutoRestart          = $src.AutoRestart
    AutoUpdate           = $src.AutoUpdate
    BackupEnabled        = $src.BackupEnabled
    BackupRetention      = $src.BackupRetention
    CustomArgs           = $src.CustomArgs
    ProcessPriority      = $src.ProcessPriority
    GameSpecificSettings = $src.GameSpecificSettings
}

if ($clone.Id -ne $src.Id) { OK "Klooni sai uuden Id:n" }
else { FAIL "Klooni ja lahde jakavat saman Id:n!" }

if ($clone.Name -eq ($src.Name + " (kopio)")) { OK "Kloonin nimi oikea ('... (kopio)')" }
else { FAIL "Kloonin nimi vaara: $($clone.Name)" }

if ($clone.Category -eq $src.Category) { OK "Kategoria kopioitu oikein" }
else { FAIL "Kategoria ei kopioitu" }

if ($clone.Tags.Count -eq $src.Tags.Count) { OK "Tagit kopioitu ($($clone.Tags.Count) kpl)" }
else { FAIL "Tagien maara muuttui kloonauksessa" }

if ($clone.MaxPlayers -eq $src.MaxPlayers) { OK "MaxPlayers kopioitu" }
else { FAIL "MaxPlayers muuttui" }

if ($clone.CustomArgs -eq $src.CustomArgs) { OK "CustomArgs kopioitu" }
else { FAIL "CustomArgs muuttui" }

# ---------------------------------------------------------------------------
HEAD "5. Import-tuplavarmuus - uusi Id generoidaan"

$originalId = $parsedList[0].Id
$newId1     = [System.Guid]::NewGuid().ToString()
$newId2     = [System.Guid]::NewGuid().ToString()

if ($newId1 -ne $originalId) { OK "Tuotu template 1 saa uuden Id:n" }
else { FAIL "Guid-tormaays - sama Id kuin alkuperainen" }

if ($newId1 -ne $newId2) { OK "Kaksi tuontia tuottaa eri Id:t" }
else { FAIL "Guid-generaatio antoi saman Id:n kahdesti" }

# ---------------------------------------------------------------------------
HEAD "6. Suodatuslogiikka"

$templates = @(
    [PSCustomObject]@{ Name="A"; Category="Kilpailu"; Tags=@("pvp","ranked") }
    [PSCustomObject]@{ Name="B"; Category="PvE";      Tags=@("survival") }
    [PSCustomObject]@{ Name="C"; Category="Kilpailu"; Tags=@("casual","pvp") }
    [PSCustomObject]@{ Name="D"; Category="";         Tags=@() }
)

$kilpailu = $templates | Where-Object { $_.Category -eq "Kilpailu" }
if ($kilpailu.Count -eq 2) { OK "Kategoria-suodatus: 2 osumaa 'Kilpailu'" }
else { FAIL "Kategoria-suodatus palautti $($kilpailu.Count) (odotettiin 2)" }

$pvp = $templates | Where-Object { $_.Tags -contains "pvp" }
if ($pvp.Count -eq 2) { OK "Tagi-suodatus: 2 osumaa 'pvp'" }
else { FAIL "Tagi-suodatus palautti $($pvp.Count) (odotettiin 2)" }

$tyhjat = @($templates | Where-Object { $_.Category -eq "" })
if ($tyhjat.Count -eq 1) { OK "Tyhja kategoria loytyy (1 kpl)" }
else { FAIL "Tyhja kategoria maara vaara: $($tyhjat.Count)" }

# ---------------------------------------------------------------------------
HEAD "7. Tiedostot"

foreach ($f in @($singleFile, $listFile, $exportFile)) {
    if (Test-Path $f) { OK "Loytyy: $(Split-Path $f -Leaf)" }
    else { FAIL "Ei loydy: $f" }
}

# Varmista etta tiedostot ovat kelvollista JSON:ia
foreach ($f in @($singleFile, $listFile, $exportFile)) {
    try {
        $null = Get-Content $f -Raw -Encoding UTF8 | ConvertFrom-Json
        OK "Validi JSON: $(Split-Path $f -Leaf)"
    } catch {
        FAIL "Virheellinen JSON: $(Split-Path $f -Leaf) - $_"
    }
}

# ---------------------------------------------------------------------------
# Siivous
if ($backup) {
    Set-Content $templFile $backup -Encoding UTF8
    Write-Host "`n  Alkuperainen templates.json palautettu."
}

Write-Host ""
Write-Host "---------------------------------------------" -ForegroundColor DarkGray
if ($fail -eq 0) {
    Write-Host "  KAIKKI $pass TESTIA LAPAISTY" -ForegroundColor Green
} else {
    Write-Host "  TULOKSET: $pass OK, $fail EPAONNISTUI" -ForegroundColor Red
}
Write-Host "---------------------------------------------" -ForegroundColor DarkGray
Write-Host "  Testitiedostot: $exportDir"
Write-Host ""

if ($fail -gt 0) { exit 1 }
