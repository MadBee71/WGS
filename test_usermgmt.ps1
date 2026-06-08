# WGS v1.0.4 - Kayttajahallinta testiskripti
# Suorita: .\test_usermgmt.ps1

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$pass = 0; $fail = 0

function OK($msg)  { Write-Host "  [OK]  $msg" -ForegroundColor Green;  $script:pass++ }
function FAIL($msg){ Write-Host "  [ERR] $msg" -ForegroundColor Red;    $script:fail++ }
function HEAD($msg){ Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

# ---------------------------------------------------------------------------
HEAD "1. UserService - tietokantarakenne"
$svcContent = Get-Content "E:\WindowsGameServer\WGS\Services\UserService.cs" -Raw -Encoding UTF8

$schemaChecks = @{
    "users-taulu"          = "CREATE TABLE IF NOT EXISTS users"
    "audit_log-taulu"      = "CREATE TABLE IF NOT EXISTS audit_log"
    "password_hash-sarake" = "password_hash"
    "role-sarake"          = "role"
    "token-sarake"         = "token"
    "is_enabled-sarake"    = "is_enabled"
    "last_login-sarake"    = "last_login"
    "audit-indeksi"        = "idx_audit_ts"
    "Migraatio last_login" = "ALTER TABLE users ADD COLUMN last_login"
    "Admin-oletustili"     = "CreateUser.*admin.*admin"
}
foreach ($n in $schemaChecks.Keys) {
    if ($svcContent -match $schemaChecks[$n]) { OK $n }
    else                                       { FAIL $n }
}

# ---------------------------------------------------------------------------
HEAD "2. PBKDF2-salasanatiiviste"

$hashChecks = @{
    "PBKDF2-SHA256"      = "Rfc2898DeriveBytes.Pbkdf2"
    "100000 iteraatiota" = "100_000"
    "SHA256"             = "HashAlgorithmName.SHA256"
    "16-tavuinen suola"  = "GetBytes\(16\)"
    "FixedTimeEquals"    = "CryptographicOperations.FixedTimeEquals"
    "Base64-tallennus"   = "Convert.ToBase64String"
}
foreach ($n in $hashChecks.Keys) {
    if ($svcContent -match $hashChecks[$n]) { OK $n }
    else                                     { FAIL $n }
}

# ---------------------------------------------------------------------------
HEAD "3. CRUD-metodit"

$crudChecks = @{
    "CreateUser"        = "public void CreateUser"
    "GetAll"            = "public List<WgsUser> GetAll"
    "ValidateToken"     = "public bool ValidateToken"
    "ValidatePassword"  = "public bool ValidatePassword"
    "ChangePassword"    = "public void ChangePassword"
    "ChangeRole"        = "public void ChangeRole"
    "RegenerateToken"   = "public void RegenerateToken"
    "SetEnabled"        = "public void SetEnabled"
    "DeleteUser"        = "public void DeleteUser"
    "GetAuditLog"       = "public List<AuditEntry> GetAuditLog"
    "WriteAudit"        = "public void WriteAudit"
    "RecordLogin"       = "RecordLogin"
}
foreach ($n in $crudChecks.Keys) {
    if ($svcContent -match [regex]::Escape($crudChecks[$n])) { OK $n }
    else                                                       { FAIL $n }
}

# ---------------------------------------------------------------------------
HEAD "4. Roolikohtainen paasynhallinta (WebApiService)"

$webContent = Get-Content "E:\WindowsGameServer\WGS\Services\WebApiService.cs" -Raw -Encoding UTF8

$roleChecks = @{
    "UserService injektoitu"       = "UserService\? Users"
    "ValidateToken-kutsu"          = "Users.ValidateToken"
    "isViewer-muuttuja"            = "isViewer"
    "Viewer-kielto writeActions"   = "writeActions"
    "403 Forbidden"                = "403"
    "Viewer cannot modify"         = "Viewer role cannot modify"
    "Audit: konsolikomento"        = "console_cmd"
    "Audit: server action"         = "WriteAudit.*action"
    "Fallback single-token"        = "Token.Equals"
}
foreach ($n in $roleChecks.Keys) {
    if ($webContent -match $roleChecks[$n]) { OK $n }
    else                                     { FAIL $n }
}

# ---------------------------------------------------------------------------
HEAD "5. MainViewModel - komennot"

$mvmContent = Get-Content "E:\WindowsGameServer\WGS\ViewModels\MainViewModel.cs" -Raw -Encoding UTF8

$cmdChecks = @{
    "AddUser-komento"              = "private void AddUser"
    "DeleteUser-komento"           = "private void DeleteUser"
    "RegenerateToken-komento"      = "private void RegenerateToken"
    "ToggleEnabled-komento"        = "private void ToggleEnabled"
    "ChangeUserRole-komento"       = "private void ChangeUserRole"
    "ChangeUserPassword-komento"   = "private void ChangeUserPassword"
    "RefreshAuditLog-komento"      = "private void RefreshAuditLog"
    "AuditLog-property"            = "public List<Services.AuditEntry> AuditLog"
    "Users-wire WebApi"            = "_webApi.Users"
    "RefreshUsers-apumetodi"       = "private void RefreshUsers"
    "Vahvistusvalintaikkuna"       = "MessageBox.Show"
}
foreach ($n in $cmdChecks.Keys) {
    if ($mvmContent -match $cmdChecks[$n]) { OK $n }
    else                                    { FAIL $n }
}

# ---------------------------------------------------------------------------
HEAD "6. Settings UI - elementit"

$xamlContent = Get-Content "E:\WindowsGameServer\WGS\Views\SettingsView.xaml" -Raw -Encoding UTF8

$uiChecks = @{
    "RoleLabel-binding"            = "RoleLabel"
    "StatusLabel-binding"          = "StatusLabel"
    "LastLoginText-binding"        = "LastLoginText"
    "ChangeUserRole-nappi"         = "ChangeUserRoleCommand"
    "ToggleEnabled-nappi"          = "ToggleEnabledCommand"
    "RegenerateToken-nappi"        = "RegenerateTokenCommand"
    "DeleteUser-nappi"             = "DeleteUserCommand"
    "ChangePassword-nappi"         = "ChangeUserPasswordCommand"
    "Audit-lista"                  = "AuditLog"
    "TimestampText-binding"        = "TimestampText"
    "Action-binding"               = '"{Binding Action}"'
    "Detail-binding"               = '"{Binding Detail}"'
    "Aktivoi-DataTrigger"          = "Aktivoi"
    "Poista-kayttajakaytoasta"     = "Poista"
}
foreach ($n in $uiChecks.Keys) {
    if ($xamlContent -match [regex]::Escape($uiChecks[$n])) { OK $n }
    else                                                      { FAIL $n }
}

# ---------------------------------------------------------------------------
HEAD "7. WgsUser-malli"

$modelChecks = @{
    "RoleLabel-property"   = "public string RoleLabel"
    "StatusLabel-property" = "public string StatusLabel"
    "LastLoginText-property"= "public string LastLoginText"
    "LastLogin-kentta"     = "public DateTime\? LastLogin"
    "UserRole.Admin"       = "UserRole.Admin"
    "UserRole.Viewer"      = "UserRole.Viewer"
}
foreach ($n in $modelChecks.Keys) {
    if ($svcContent -match $modelChecks[$n]) { OK $n }
    else                                      { FAIL $n }
}

# ---------------------------------------------------------------------------
HEAD "8. AuditEntry-malli"

$auditChecks = @{
    "AuditEntry-luokka"    = "public class AuditEntry"
    "Username-kentta"      = "public string   Username"
    "Action-kentta"        = "public string   Action"
    "Detail-kentta"        = "public string   Detail"
    "Timestamp-kentta"     = "public DateTime"
    "TimestampText"        = "public string   TimestampText"
}
foreach ($n in $auditChecks.Keys) {
    if ($svcContent -match $auditChecks[$n]) { OK $n }
    else                                      { FAIL $n }
}

# ---------------------------------------------------------------------------
HEAD "9. Audit-tapahtumatyypit"

$auditActions = @("login", "login_fail", "create_user", "delete_user",
    "change_password", "change_role", "regen_token", "enable_user",
    "disable_user", "console_cmd")
foreach ($a in $auditActions) {
    if ($svcContent -match "`"$a`"" -or $webContent -match "`"$a`"" -or $mvmContent -match "`"$a`"") {
        OK "Auditoi: $a"
    } else {
        FAIL "Auditointityyppi puuttuu: $a"
    }
}

# ---------------------------------------------------------------------------
HEAD "10. Buildi"

$buildOut = & dotnet build "E:\WindowsGameServer\WGS\WGS.csproj" --no-restore -p:TreatWarningsAsErrors=false 2>&1
if ($buildOut | Select-String "^Build succeeded") { OK "dotnet build onnistui" }
else { FAIL "Build epaonnistui:`n$($buildOut | Select-String 'error' | Select-Object -First 3)" }

# ---------------------------------------------------------------------------
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
