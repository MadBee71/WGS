# WGS Server Diagnostiikka
# Kopioi tama ja WindowsGameServer.exe samaan kansioon serverikoneella
# Aja PowerShellissa: .\diagnose_server.ps1

Write-Host "`n=== WGS Server Diagnostiikka ===" -ForegroundColor Cyan

# 1. Windows-versio
$os = Get-CimInstance Win32_OperatingSystem
Write-Host "`n[OS] $($os.Caption) Build $($os.BuildNumber) ($($os.OSArchitecture))"

# 2. .NET-versiot
Write-Host "`n[.NET] Asennetut runtimet:"
$runtimes = & dotnet --list-runtimes 2>&1
if ($LASTEXITCODE -eq 0) {
    $runtimes | ForEach-Object { Write-Host "  $_" }
    $hasDesktop = $runtimes | Where-Object { $_ -match "Microsoft.WindowsDesktop.App 8\." }
    $hasRuntime = $runtimes | Where-Object { $_ -match "Microsoft.NETCore.App 8\." }
    if ($hasDesktop) { Write-Host "  [OK] Windows Desktop Runtime 8.x loytyy" -ForegroundColor Green }
    else             { Write-Host "  [PUUTTUU] Windows Desktop Runtime 8.x EI loydy" -ForegroundColor Red
                       Write-Host "  -> Tarvitaan: https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe" -ForegroundColor Yellow }
    if (-not $hasRuntime) { Write-Host "  [PUUTTUU] .NET Runtime 8.x ei loydy" -ForegroundColor Red }
} else {
    Write-Host "  [VIRHE] dotnet-komentoa ei loydy - .NET ei ole asennettu tai ei PATH:issa" -ForegroundColor Red
}

# 3. Kaynnista exe ja nappaa virhe
$exePath = Join-Path $PSScriptRoot "WindowsGameServer.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "`n[EXE] WindowsGameServer.exe ei loydy samasta kansiosta" -ForegroundColor Red
} else {
    $exeSize = [math]::Round((Get-Item $exePath).Length / 1MB, 1)
    Write-Host "`n[EXE] Loydetty: $exePath ($exeSize MB)"

    Write-Host "`n[KAYNNISTYS] Yritetaan kaynnistaa konsolissa..."
    $proc = Start-Process -FilePath $exePath -PassThru -Wait `
        -RedirectStandardError "$env:TEMP\wgs_stderr.txt" 2>&1
    $exitCode = $proc.ExitCode
    Write-Host "  Exit code: $exitCode"

    $stderr = Get-Content "$env:TEMP\wgs_stderr.txt" -ErrorAction SilentlyContinue
    if ($stderr) {
        Write-Host "  STDERR:" -ForegroundColor Red
        $stderr | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
    }
}

# 4. Event Log - viimeiset WGS/Application-virheet
Write-Host "`n[EVENT LOG] Viimeiset .NET-virheet (viimeiset 5 min):"
$since = (Get-Date).AddMinutes(-5)
$events = Get-EventLog -LogName Application -EntryType Error -After $since -ErrorAction SilentlyContinue |
          Where-Object { $_.Source -match "\.NET|Application Error|WindowsGameServer|dotnet" } |
          Select-Object -First 5
if ($events) {
    $events | ForEach-Object {
        Write-Host "  [$($_.TimeGenerated)] $($_.Source): $($_.Message.Substring(0, [Math]::Min(200, $_.Message.Length)))" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Ei virheita viimeiselta 5 minuutilta"
}

# 5. Temp-kansion oikeudet (single-file purkautuu sinne)
Write-Host "`n[TEMP] Kirjoitusoikeus %TEMP%-kansioon:"
$testFile = "$env:TEMP\wgs_write_test.tmp"
try {
    [System.IO.File]::WriteAllText($testFile, "test")
    Remove-Item $testFile
    Write-Host "  [OK] Kirjoitusoikeus kunnossa" -ForegroundColor Green
} catch {
    Write-Host "  [VIRHE] Ei kirjoitusoikeutta: $_" -ForegroundColor Red
}

# .NET single-file purkautuu tanne:
$extractDir = "$env:TEMP\.net"
Write-Host "`n[SINGLE-FILE] Purkuhakemisto: $extractDir"
if (Test-Path $extractDir) {
    $wgsExtract = Get-ChildItem $extractDir -Filter "WindowsGameServer*" -ErrorAction SilentlyContinue
    if ($wgsExtract) { Write-Host "  [OK] WGS on jo purettu: $($wgsExtract.FullName)" -ForegroundColor Green }
    else             { Write-Host "  Hakemisto on olemassa mutta WGS:a ei ole vielä purettu" }
} else {
    Write-Host "  Hakemistoa ei ole - exe ei ole kaynnistynyt kertaakaan"
}

# 6. Antivirus / SmartScreen
Write-Host "`n[TIETOTURVA] Zone.Identifier (SmartScreen-esto):"
$zoneFile = "$exePath`:Zone.Identifier"
if (Test-Path $zoneFile) {
    $zone = Get-Content $zoneFile -ErrorAction SilentlyContinue
    Write-Host "  [ESTO] Tiedostolla on Zone.Identifier - Windows SmartScreen voi estaa:" -ForegroundColor Yellow
    $zone | ForEach-Object { Write-Host "    $_" }
    Write-Host "  -> Korjaus: Unblock-File '$exePath'" -ForegroundColor Yellow
} else {
    Write-Host "  [OK] Ei Zone.Identifier-estoa" -ForegroundColor Green
}

Write-Host "`n=================================" -ForegroundColor Cyan
Write-Host "Kopioi taman skriptin tulostus ja laheta se kehittajalle." -ForegroundColor White
Write-Host ""
