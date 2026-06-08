# WGS julkaisuskripti
# Kaytto: .\publish.ps1 [-Version "1.0.4"] [-SkipBuild]
#
# Tekee:
#   1. Kasvattaa version WGS.csproj:ssa (tai kayttaa annettua)
#   2. Buildaa Release + PublishSingleFile
#   3. Kopioi exe:n -> publish_out\
#   4. Luo pakatun zip:n  -> github\WGS-v<versio>.zip
#      (kansiorakenne sisalla: WGS-v<versio>\WindowsGameServer.exe + README + LICENSE)

param(
    [string]$Version   = "",      # Jos tyhja, lukee csprojista + kasvattaa patch
    [switch]$SkipBuild            # Vain pakkaus, ei buildia
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root      = "E:\WindowsGameServer"
$csproj    = "$root\WGS\WGS.csproj"
$publishDir = "$root\WGS\bin\Release\net8.0-windows\publish"
$outDir    = "$root\publish_out"
$githubDir = "$root\github"

function Step($msg) { Write-Host "`n>>> $msg" -ForegroundColor Cyan }
function OK($msg)   { Write-Host "    [OK] $msg" -ForegroundColor Green }
function FAIL($msg) { Write-Host "    [ERR] $msg" -ForegroundColor Red; exit 1 }

# ── 1. Versio ────────────────────────────────────────────────────────────────
Step "Versio"

$xml = [xml](Get-Content $csproj -Encoding UTF8)
$currentVer = $xml.Project.PropertyGroup[0].Version
Write-Host "    Nykyinen versio: $currentVer"

if ($Version -eq "") {
    # Kasvata patch automaattisesti
    $parts = $currentVer.Split('.')
    $parts[2] = ([int]$parts[2] + 1).ToString()
    $Version = $parts -join '.'
    Write-Host "    Uusi versio (auto patch): $Version"
} else {
    Write-Host "    Annettu versio: $Version"
}

if (-not $SkipBuild) {
    # Paivita csproj
    $content = Get-Content $csproj -Raw -Encoding UTF8
    $content = $content -replace '<Version>[^<]+</Version>',           "<Version>$Version</Version>"
    $content = $content -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
    $content = $content -replace '<FileVersion>[^<]+</FileVersion>',   "<FileVersion>$Version.0</FileVersion>"
    $content = $content -replace '<InformationalVersion>[^<]+</InformationalVersion>', "<InformationalVersion>$Version</InformationalVersion>"
    Set-Content $csproj $content -Encoding UTF8
    OK "csproj paivitetty -> $Version"
}

# ── 2. Build ─────────────────────────────────────────────────────────────────
if (-not $SkipBuild) {
    Step "Build + Publish (Release, single-file)"

    $buildOut = dotnet publish "$root\WGS\WGS.csproj" `
        -c Release `
        -r win-x64 `
        --self-contained false `
        -p:PublishSingleFile=true `
        -p:TreatWarningsAsErrors=false `
        2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host $buildOut
        FAIL "Build epaonnistui (exit $LASTEXITCODE)"
    }
    OK "Build valmis"
}

# ── 3. Tarkista exe ───────────────────────────────────────────────────────────
Step "Tarkista julkaistuexe"

$exeSrc = "$publishDir\WindowsGameServer.exe"
if (-not (Test-Path $exeSrc)) {
    # Etsi publish-hakemisto automaattisesti
    $found = Get-ChildItem "$root\WGS\bin\Release" -Recurse -Filter "WindowsGameServer.exe" |
             Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($found) {
        $exeSrc = $found.FullName
        Write-Host "    Exe loydetty: $exeSrc"
    } else {
        FAIL "WindowsGameServer.exe ei loydy publish-hakemistosta"
    }
}
OK "Exe: $exeSrc ($([math]::Round((Get-Item $exeSrc).Length / 1MB, 1)) MB)"

# ── 4. Kopioi publish_out ────────────────────────────────────────────────────
Step "Kopioi -> publish_out\"

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$destExe = "$outDir\WindowsGameServer.exe"
Copy-Item $exeSrc $destExe -Force
OK "Kopioitu: $destExe"

# ── 5. Luo github-zip ────────────────────────────────────────────────────────
Step "Luo github\WGS-v$Version.zip"

New-Item -ItemType Directory -Force -Path $githubDir | Out-Null

$zipName   = "WGS_v${Version}_$(Get-Date -Format 'yyyyMMdd').zip"
$zipPath   = "$githubDir\$zipName"
$stagingDir = "$env:TEMP\wgs_release_staging\WGS-v$Version"

# Poista vanhat staging-tiedostot
Remove-Item -Recurse -Force "$env:TEMP\wgs_release_staging" -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null

# Kopioi sisalto zip:iin
Copy-Item $exeSrc "$stagingDir\WindowsGameServer.exe" -Force

foreach ($extra in @("README.md", "LICENSE")) {
    $src = "$root\$extra"
    if (Test-Path $src) { Copy-Item $src "$stagingDir\$extra" -Force }
}

# Pakkaa
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
    Write-Host "    Poistettu vanha: $zipName"
}

Compress-Archive -Path "$env:TEMP\wgs_release_staging\*" -DestinationPath $zipPath
Remove-Item -Recurse -Force "$env:TEMP\wgs_release_staging" -ErrorAction SilentlyContinue

$zipSizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
OK "Luotu: $zipPath ($zipSizeMB MB)"

# ── 6. Yhteenveto ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=============================================" -ForegroundColor DarkGray
Write-Host "  WGS v$Version julkaistu!" -ForegroundColor Green
Write-Host "  exe  : $destExe" -ForegroundColor White
Write-Host "  zip  : $zipPath" -ForegroundColor White
Write-Host "=============================================" -ForegroundColor DarkGray
Write-Host ""
