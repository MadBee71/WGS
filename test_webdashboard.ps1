# WGS v1.0.4 - Web Dashboard testiskripti
# Suorita: .\test_webdashboard.ps1
# Testaa API-logiikan ja HTML-rakenteen ilman kaynnissa olevaa serveria.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$pass = 0; $fail = 0

function OK($msg)  { Write-Host "  [OK]  $msg" -ForegroundColor Green;  $script:pass++ }
function FAIL($msg){ Write-Host "  [ERR] $msg" -ForegroundColor Red;    $script:fail++ }
function HEAD($msg){ Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

# ---------------------------------------------------------------------------
HEAD "1. HTML-rakenne"

$htmlFile = "$env:TEMP\wgs_dashboard_test.html"

# Rakenna HTML testia varten (simuloi BuildUiHtml-logiikka)
$html = Get-Content "E:\WindowsGameServer\WGS\Services\WebApiService.cs" -Raw -Encoding UTF8

# Etsi raw string literal alku ja loppu
$start = $html.IndexOf('<!DOCTYPE html>')
$end   = $html.IndexOf('""";', $start)
if ($start -lt 0 -or $end -lt 0) { FAIL "HTML raw string literal ei loydy palvelusta"; exit 1 }
$dashHtml = $html.Substring($start, $end - $start)
Set-Content $htmlFile $dashHtml -Encoding UTF8

# Tarkista pakolliset elementit
$checks = @{
    "Auth-lomake (tokInp)"     = 'id="tokInp"'
    "Yhdista-nappi"            = 'connect()'
    "appWrap-div"              = 'id="appWrap"'
    "authWrap-div"             = 'id="authWrap"'
    "CPU-metriikka"            = 'id="sCpu"'
    "Muisti-metriikka"         = 'id="sMem"'
    "Kaista alas"              = 'id="sNetIn"'
    "Kaista ylos"              = 'id="sNetOut"'
    "Palvelinruudukko"         = 'id="srvGrid"'
    "Start-nappi"              = "'start'"
    "Stop-nappi"               = "'stop'"
    "Restart-nappi"            = "'restart'"
    "Update-nappi"             = "'update'"
    "Backup-nappi"             = "'backup'"
    "Konsolikomento-kentta"    = "c_"
    "Loki-toggle"              = "toggleLog"
    "Loki-box"                 = "log-box"
    "Log-poller"               = "pollLog"
    "fetchDetail-funktio"      = "fetchDetail"
    "Uptime-elementti"         = "up_"
    "Pelaajariivi"             = "plrow_"
    "Toast-elementti"          = 'id="toast"'
    "localStorage-token"       = "localStorage"
    "Auto-refresh (setInterval)"= "setInterval"
    "API-funktio"              = "async function api("
    "loadAll-funktio"          = "async function loadAll"
    "fetch-kutsu API:lle"       = "fetch('/api/'"
}

foreach ($name in $checks.Keys) {
    if ($dashHtml -match [regex]::Escape($checks[$name])) { OK $name }
    else                                                    { FAIL "Puuttuu: $name ('$($checks[$name])')" }
}

# ---------------------------------------------------------------------------
HEAD "2. API-endpoint-rakenne (WebApiService.cs)"

$svcContent = Get-Content "E:\WindowsGameServer\WGS\Services\WebApiService.cs" -Raw -Encoding UTF8

$apiChecks = @{
    "GET /api/servers"         = '"/api/servers"'
    "GET /api/servers/{id}"    = '/api/servers/([^/]+)'
    "GET /api/system"          = '"/api/system"'
    "GET /api/servers/{id}/log"= '"log"'
    "POST start"               = '"start"'
    "POST stop"                = '"stop"'
    "POST restart"             = '"restart"'
    "POST backup"              = '"backup"'
    "POST cmd"                 = '"cmd"'
    "401 auth check"           = "401"
    "Bearer token"             = "Bearer"
    "GetUptime callback"       = "GetUptime"
    "GetOnlinePlayers callback"= "GetOnlinePlayers"
    "Uptime in response"       = "Uptime"
    "Players in response"      = "Players"
    "CORS header"              = "Access-Control-Allow-Origin"
    "OPTIONS preflight"        = '"OPTIONS"'
}

foreach ($name in $apiChecks.Keys) {
    if ($svcContent -match $apiChecks[$name]) { OK "API: $name" }
    else                                       { FAIL "API puuttuu: $name" }
}

# ---------------------------------------------------------------------------
HEAD "3. MainViewModel wire-up"

$mvmContent = Get-Content "E:\WindowsGameServer\WGS\ViewModels\MainViewModel.cs" -Raw -Encoding UTF8

$wireChecks = @{
    "GetUptime wired"          = "_webApi.GetUptime"
    "GetOnlinePlayers wired"   = "_webApi.GetOnlinePlayers"
    "GetServers wired"         = "_webApi.GetServers"
    "GetMetrics wired"         = "_webApi.GetMetrics"
    "GetNetwork wired"         = "_webApi.GetNetwork"
    "GetLog wired"             = "_webApi.GetLog"
}

foreach ($name in $wireChecks.Keys) {
    if ($mvmContent -match [regex]::Escape($wireChecks[$name])) { OK $name }
    else                                                          { FAIL "Puuttuu wire-up: $name" }
}

# ---------------------------------------------------------------------------
HEAD "4. Tietoturva: Bearer token -autentikointi"

# Tarkista etta autentikointi on pakollinen API-kutsuis
if ($svcContent -match "path\.StartsWith.*api" -and $svcContent -match "401") {
    OK "Bearer token -tarkistus loytyy"
} else {
    FAIL "Bearer token -tarkistus puuttuu"
}

if ($svcContent -match "Unauthorized") { OK "401 Unauthorized-viesti loytyy" }
else                                    { FAIL "401-viesti puuttuu" }

# UI: localStorage token-tallennukselle
if ($dashHtml -match "localStorage.setItem") { OK "Token tallennetaan localStorageen" }
else                                          { FAIL "localStorage.setItem puuttuu" }

# ---------------------------------------------------------------------------
HEAD "5. Log viewer -logiikka"

if ($dashHtml -match "nextOffset")        { OK "nextOffset-paginointi loytyy" }
else                                       { FAIL "nextOffset puuttuu" }

if ($dashHtml -match "logOffsets")        { OK "Offset-tila per palvelin loytyy" }
else                                       { FAIL "logOffsets-tila puuttuu" }

if ($dashHtml -match "300")               { OK "Max 300 loki-rivia rajoitus loytyy" }
else                                       { FAIL "Loki-rivirajoitus puuttuu" }

if ($dashHtml -match "scrollTop.*scrollHeight") { OK "Auto-scroll loppuun loytyy" }
else                                             { FAIL "Auto-scroll puuttuu" }

if ($dashHtml -match "log-line.*System")  { OK "System-tyypin korostus loytyy" }
else                                       { FAIL "System-tyypin korostus puuttuu" }

if ($dashHtml -match "log-line.*Error")   { OK "Error-tyypin korostus loytyy" }
else                                       { FAIL "Error-tyypin korostus puuttuu" }

# ---------------------------------------------------------------------------
HEAD "6. Responsiivisuus"

if ($dashHtml -match "max-width:600px")   { OK "Mobiilibreakpoint (600px) loytyy" }
else                                       { FAIL "Mobiilibreakpoint puuttuu" }

if ($dashHtml -match "auto-fill")         { OK "CSS Grid auto-fill loytyy" }
else                                       { FAIL "CSS Grid puuttuu" }

if ($dashHtml -match "flex-wrap")         { OK "flex-wrap mobiilituella" }
else                                       { FAIL "flex-wrap puuttuu" }

# ---------------------------------------------------------------------------
HEAD "7. Buildi"

$buildOut = & dotnet build "E:\WindowsGameServer\WGS\WGS.csproj" --no-restore -p:TreatWarningsAsErrors=false 2>&1
$buildOk  = $buildOut | Select-String "^Build succeeded"
if ($buildOk) { OK "dotnet build onnistui" }
else           { FAIL "dotnet build epaonnistui:`n$($buildOut | Select-String 'error CS' | Select-Object -First 3)" }

# ---------------------------------------------------------------------------
Remove-Item $htmlFile -ErrorAction SilentlyContinue

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
