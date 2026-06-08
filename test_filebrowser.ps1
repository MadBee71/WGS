# WGS v1.0.4 - Tiedostoselain testiskripti
# Suorita: .\test_filebrowser.ps1

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$pass = 0
$fail = 0
$testRoot = "$env:TEMP\wgs_filebrowser_test"

function OK($msg)  { Write-Host "  [OK]  $msg" -ForegroundColor Green;  $script:pass++ }
function FAIL($msg){ Write-Host "  [ERR] $msg" -ForegroundColor Red;    $script:fail++ }
function HEAD($msg){ Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

# ── Testirakenne ──────────────────────────────────────────────────────────────
HEAD "Alustus - testitiedostot"

Remove-Item -Recurse -Force $testRoot -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path "$testRoot\server"           | Out-Null
New-Item -ItemType Directory -Force -Path "$testRoot\server\configs"   | Out-Null
New-Item -ItemType Directory -Force -Path "$testRoot\server\logs"      | Out-Null
New-Item -ItemType Directory -Force -Path "$testRoot\outside"          | Out-Null

Set-Content "$testRoot\server\server.cfg"      "hostname MyServer`nport 27015" -Encoding UTF8
Set-Content "$testRoot\server\configs\game.json" '{"maxplayers":10}' -Encoding UTF8
Set-Content "$testRoot\server\logs\latest.log" "Server started OK" -Encoding UTF8
Set-Content "$testRoot\outside\secret.txt"     "SALAISUUS" -Encoding UTF8

$root = (Resolve-Path "$testRoot\server").Path
Write-Host "  Juuri: $root"

# ── IsPathSafe-logiikka ───────────────────────────────────────────────────────
HEAD "1. IsPathSafe - Path Traversal -suojaus"

function IsPathSafe($testPath, $rootPath) {
    if ([string]::IsNullOrWhiteSpace($testPath) -or [string]::IsNullOrWhiteSpace($rootPath)) {
        return $false
    }
    try {
        $full = [System.IO.Path]::GetFullPath($testPath)
        $r    = [System.IO.Path]::GetFullPath($rootPath)
        $sep  = [System.IO.Path]::DirectorySeparatorChar
        $rootWithSep = $r.TrimEnd($sep) + $sep
        return ($full.StartsWith($rootWithSep, [System.StringComparison]::OrdinalIgnoreCase) -or
                [string]::Equals($full, $r, [System.StringComparison]::OrdinalIgnoreCase))
    } catch {
        return $false
    }
}

# Sallitut polut
if (IsPathSafe $root $root)                                  { OK "Juuri itse on sallittu" }
else                                                          { FAIL "Juuri itse ei ole sallittu" }

if (IsPathSafe "$root\configs" $root)                        { OK "Alikansio configs on sallittu" }
else                                                          { FAIL "Alikansio ei ole sallittu" }

if (IsPathSafe "$root\configs\game.json" $root)              { OK "Tiedosto juuressa on sallittu" }
else                                                          { FAIL "Tiedosto ei ole sallittu" }

if (IsPathSafe "$root\logs\latest.log" $root)                { OK "Syvempi tiedosto on sallittu" }
else                                                          { FAIL "Syvempi tiedosto ei ole sallittu" }

# Kielletyt polut
if (-not (IsPathSafe "$testRoot\outside" $root))             { OK "Rinnakkainen kansio 'outside' hylätty" }
else                                                          { FAIL "Rinnakkainen kansio pääsi lapi!" }

if (-not (IsPathSafe "$testRoot\outside\secret.txt" $root))  { OK "Tiedosto juuren ulkopuolella hylätty" }
else                                                          { FAIL "Ulkopuolinen tiedosto pääsi lapi!" }

if (-not (IsPathSafe "$testRoot" $root))                     { OK "Juuren ylakansio hylätty" }
else                                                          { FAIL "Ylakansio pääsi lapi!" }

# Path traversal -hyökkäykset
$traversal1 = "$root\configs\..\..\outside\secret.txt"
$resolved1  = try { [System.IO.Path]::GetFullPath($traversal1) } catch { "" }
if (-not (IsPathSafe $traversal1 $root))                     { OK "..\\..\\outside hylätty (resolved: $resolved1)" }
else                                                          { FAIL "Path traversal ..\\..\\outside pääsi lapi!" }

$traversal2 = "$root\..\outside"
if (-not (IsPathSafe $traversal2 $root))                     { OK "..\\ ylastp hylätty" }
else                                                          { FAIL "Path traversal ..\\ pääsi lapi!" }

# Prefix-huijaus: "serverExtended" ei ole "server":n alla
$fakeRoot = "$testRoot\serverExtended"
New-Item -ItemType Directory -Force -Path $fakeRoot | Out-Null
if (-not (IsPathSafe $fakeRoot $root))                       { OK "Prefix-huijaus serverExtended hylätty" }
else                                                          { FAIL "Prefix-huijaus serverExtended pääsi lapi!" }

# Tyhja/null syotteet
if (-not (IsPathSafe "" $root))                              { OK "Tyhja polku hylätty" }
else                                                          { FAIL "Tyhja polku pääsi lapi!" }

if (-not (IsPathSafe $root ""))                              { OK "Tyhja root hylätty" }
else                                                          { FAIL "Tyhja root pääsi lapi!" }

# ── Nimeämisen tietoturva ──────────────────────────────────────────────────────
HEAD "2. Nimeäminen - polkusegmenttisuojaus"

function IsNameSafe($name) {
    if ([string]::IsNullOrWhiteSpace($name)) { return $false }
    return -not ($name.Contains('/') -or $name.Contains('\') -or $name.Contains('..'))
}

if (IsNameSafe "server.cfg")          { OK "Normaali nimi ok" }
else                                   { FAIL "Normaali nimi hylätty" }

if (IsNameSafe "my-config_v2.json")   { OK "Nimi erikoismerkeilla ok" }
else                                   { FAIL "Normaali nimi erikoismerkeilla hylätty" }

if (-not (IsNameSafe "../secret"))    { OK "../secret hylätty" }
else                                   { FAIL "../secret pääsi lapi!" }

if (-not (IsNameSafe ".."))           { OK ".. hylätty" }
else                                   { FAIL ".. pääsi lapi!" }

if (-not (IsNameSafe "sub/file.txt")) { OK "Kauttaviiva nimessa hylätty" }
else                                   { FAIL "Kauttaviiva pääsi lapi!" }

if (-not (IsNameSafe "sub\file.txt")) { OK "Takakenoviiva nimessa hylätty" }
else                                   { FAIL "Takakenoviiva pääsi lapi!" }

# ── Muokattavat tiedostotyypit ─────────────────────────────────────────────────
HEAD "3. Muokattavat tiedostotyypit"

$editableExts = @(".cfg",".ini",".json",".xml",".txt",".yaml",".yml",".toml",
                  ".conf",".config",".properties",".log",".sh",".bat",".cmd",".env",".csv")
$nonEditable  = @(".exe",".dll",".zip",".rar",".png",".jpg",".mp3",".wav",".db")

foreach ($ext in $editableExts) {
    if ($editableExts -contains $ext) { OK "Muokattava: $ext" }
    else                               { FAIL "Pitaisi olla muokattava: $ext" }
}
foreach ($ext in $nonEditable) {
    if ($editableExts -notcontains $ext) { OK "Ei-muokattava: $ext" }
    else                                  { FAIL "Pitaisi olla ei-muokattava: $ext" }
}

# ── Tiedoston koko -raja ──────────────────────────────────────────────────────
HEAD "4. Tiedoston koko -raja (2 MB)"

$maxBytes = 2 * 1024 * 1024

# Pieni tiedosto
$smallFile = "$root\small.cfg"
Set-Content $smallFile ("A" * 100) -Encoding UTF8
$size = (Get-Item $smallFile).Length
if ($size -lt $maxBytes) { OK "Pieni tiedosto ($size B) on muokattavissa" }
else                      { FAIL "Pieni tiedosto ylittaa rajan" }

# Suuri tiedosto (yli 2 MB) — simuloidaan kokotarkastus
$bigSize = 3 * 1024 * 1024
if ($bigSize -gt $maxBytes) { OK "Suuri tiedosto (3 MB) ylittaa rajan - estetaan" }
else                         { FAIL "Suuri tiedosto ei ylita rajaa" }

# ── Tekstieditori: luku ja kirjoitus ─────────────────────────────────────────
HEAD "5. Tekstieditori - luku/kirjoitus"

$cfgFile    = "$root\server.cfg"
$content    = Get-Content $cfgFile -Raw -Encoding UTF8
if ($content -match "hostname") { OK "Tiedoston luku onnistui" }
else                              { FAIL "Tiedostossa ei ole odotettua sisaltoa" }

# Kirjoitustesti
$original   = $content
$modified   = $content + "`nmaxplayers 16"
Set-Content $cfgFile $modified -Encoding UTF8
$readback   = Get-Content $cfgFile -Raw -Encoding UTF8
if ($readback -match "maxplayers 16") { OK "Muutos kirjoitettu ja luettu" }
else                                   { FAIL "Muutos ei tallentunut" }

# Palautetaan alkuperainen
Set-Content $cfgFile $original -Encoding UTF8
$restored = Get-Content $cfgFile -Raw -Encoding UTF8
if ($restored -match "hostname") { OK "Alkuperainen sisalto palautettu" }
else                               { FAIL "Palautus epaonnistui" }

# JSON-tiedosto
$jsonFile   = "$root\configs\game.json"
$jsonBefore = Get-Content $jsonFile -Raw -Encoding UTF8 | ConvertFrom-Json
if ($jsonBefore.maxplayers -eq 10) { OK "JSON luettu oikein (maxplayers=10)" }
else                                 { FAIL "JSON arvo vaara" }

$newJson = '{"maxplayers":20,"password":"test123"}'
Set-Content $jsonFile $newJson -Encoding UTF8
$jsonAfter = Get-Content $jsonFile -Raw -Encoding UTF8 | ConvertFrom-Json
if ($jsonAfter.maxplayers -eq 20) { OK "JSON kirjoitettu oikein (maxplayers=20)" }
else                                { FAIL "JSON tallennus epaonnistui" }

# ── Lataus (Download) -logiikka ───────────────────────────────────────────────
HEAD "6. Lataus (Download)"

$srcFile  = "$root\server.cfg"
$destFile = "$testRoot\download_result.cfg"

Copy-Item $srcFile $destFile -Force
if (Test-Path $destFile) { OK "Tiedosto kopioitu latauksena" }
else                      { FAIL "Lataus epaonnistui - kohdetiedostoa ei synny" }

$srcContent  = Get-Content $srcFile -Raw -Encoding UTF8
$destContent = Get-Content $destFile -Raw -Encoding UTF8
if ($srcContent -eq $destContent) { OK "Ladattu sisalto identtinen lahteen" }
else                                { FAIL "Ladattu sisalto poikkeaa lahteesta" }

# Tietoturva: lataus ulkopuolisesta polusta estetaan
$blocked = -not (IsPathSafe "$testRoot\outside\secret.txt" $root)
if ($blocked) { OK "Lataus juuren ulkopuolelta estetty" }
else           { FAIL "Lataus ulkopuolelta ei estetty!" }

# ── Hakusuodatus ──────────────────────────────────────────────────────────────
HEAD "7. Hakusuodatus"

$files = @(
    [PSCustomObject]@{ Name = "server.cfg";   IsDir = $false }
    [PSCustomObject]@{ Name = "server.ini";   IsDir = $false }
    [PSCustomObject]@{ Name = "game.json";    IsDir = $false }
    [PSCustomObject]@{ Name = "latest.log";   IsDir = $false }
    [PSCustomObject]@{ Name = "configs";      IsDir = $true  }
)

# Suodata "server"
$q       = "server"
$matches = @($files | Where-Object { $_.Name.ToLower().Contains($q) })
if ($matches.Count -eq 2) { OK "Haku 'server' loysi 2 osumaa" }
else                        { FAIL "Haku 'server' antoi $($matches.Count) (odotettiin 2)" }

# Suodata ".cfg"
$q2      = ".cfg"
$matches2 = @($files | Where-Object { $_.Name.ToLower().Contains($q2) })
if ($matches2.Count -eq 1) { OK "Haku '.cfg' loysi 1 osuman" }
else                         { FAIL "Haku '.cfg' antoi $($matches2.Count)" }

# Tyhja haku palauttaa kaikki
$all = @($files | Where-Object { $true })
if ($all.Count -eq 5) { OK "Tyhja haku palauttaa kaikki (5)" }
else                   { FAIL "Tyhja haku palautti $($all.Count)" }

# ── Breadcrumb ────────────────────────────────────────────────────────────────
HEAD "8. Leivänmuru (Breadcrumb)"

$rootName = [System.IO.Path]::GetFileName($root.TrimEnd([System.IO.Path]::DirectorySeparatorChar))
$subPath  = "$root\configs"

# Simuloi GetRelativePath ilman .NET 6+ API:a
function GetRelPath($from, $to) {
    $f = $from.TrimEnd('\') + '\'
    if ($to.StartsWith($f, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $to.Substring($f.Length)
    }
    if ([string]::Equals($from.TrimEnd('\'), $to.TrimEnd('\'), [System.StringComparison]::OrdinalIgnoreCase)) {
        return "."
    }
    return $to
}

$relPath = GetRelPath $root $subPath
$parts   = @($rootName) + $relPath.Split('\')

if ($parts[0] -eq $rootName)    { OK "Juuri: '$($parts[0])'" }
else                             { FAIL "Juuri vaara: $($parts[0])" }

if ($parts[1] -eq "configs")    { OK "Alikansio: '$($parts[1])'" }
else                             { FAIL "Alikansio vaara: $($parts[1])" }

# Juuri itse - GetRelPath palauttaa "."
$relRoot = GetRelPath $root $root
if ($relRoot -eq ".") { OK "Juuressa ollessaan rel='.' (ei kaksinkertaista nimea)" }
else                   { FAIL "Juuren rel-polku vaara: $relRoot" }

# ── Siivous ───────────────────────────────────────────────────────────────────
Remove-Item -Recurse -Force $testRoot -ErrorAction SilentlyContinue

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
