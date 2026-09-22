; Separate installer for the optional WGS Background Service (Settings -> Service mode in the
; main WGS app). Deliberately kept OUT of WGS_Setup.iss: registering a Windows Service requires
; admin elevation (sc.exe), and the main installer is intentionally PrivilegesRequired=lowest so
; the common case (just running WGS interactively) never triggers a UAC prompt. Splitting this out
; means installing/using WGS normally is completely unaffected by this installer's existence.
#define AppName "WGS Background Service (Experimental)"
#define AppVersion "1.5.20"
#define AppPublisher "MadBee71"
#define AppURL "https://wgsserver.com"
#define AppExeName "WGS.ServiceHost.exe"
#define ServiceName "WGS Background Service"

[Setup]
AppId={{6F3B8C1A-4D2E-4F9A-B7C5-8A1D3E6F9B2C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
; Same default location as the main WGS installer (WGS_Setup.iss) on purpose — WGS.ServiceHost
; and WindowsGameServer.exe must live in the same folder to share WGS_Data\settings.json
; (ConfigService resolves data paths relative to the running exe's own folder). See the note in
; WGS.ServiceHost\Worker.cs and the WizardNotes page below.
DefaultDirName={autopf}\WGS
DefaultGroupName={#AppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=..\releases
OutputBaseFilename=WGS_ServiceHost_Setup_{#AppVersion}
SetupIconFile=..\WGS\favicon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; Installing a Windows Service requires admin rights — unlike the main WGS installer, this one
; cannot be "lowest".
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\publish_out_servicehost\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

; binPath= just needs ONE pair of quotes around the plain exe path here (no extra arguments after
; it) — verified against Microsoft's own sc-create docs (22.9.2026). The double-escaped-quotes
; pattern some guides show is only needed when binPath also carries separate quoted arguments
; after the exe path, which it doesn't here; using it anyway would have shipped a service that
; silently fails to start.
; DisplayName is set separately from ServiceName (the internal SCM key) so services.msc shows the
; "(Experimental)" label to anyone browsing their installed services, without changing the actual
; registered name Program.cs's options.ServiceName / start-stop control signals rely on matching.
[Run]
Filename: "{sys}\sc.exe"; Parameters: "create ""{#ServiceName}"" binPath= ""{app}\{#AppExeName}"" start= auto DisplayName= ""{#AppName}"""; Flags: runhidden; StatusMsg: "Registering {#ServiceName}..."
Filename: "{sys}\sc.exe"; Parameters: "description ""{#ServiceName}"" ""[EXPERIMENTAL] Keeps WGS-managed game servers running in the background, even when nobody is logged into Windows. Configure from WGS -> Settings -> Service mode. See wgsserver.com."""; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "start ""{#ServiceName}"""; Flags: runhidden; StatusMsg: "Starting {#ServiceName}..."

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop ""{#ServiceName}"""; Flags: runhidden; RunOnceId: "StopService"
Filename: "{sys}\sc.exe"; Parameters: "delete ""{#ServiceName}"""; Flags: runhidden; RunOnceId: "DeleteService"

[Code]
procedure InitializeWizard;
var
  NotePage: TWizardPage;
  NoteLabel: TNewStaticText;
begin
  NotePage := CreateCustomPage(wpWelcome, 'Before you continue', 'This installer is for an optional, experimental feature.');
  NoteLabel := TNewStaticText.Create(NotePage);
  NoteLabel.Parent := NotePage.Surface;
  NoteLabel.AutoSize := False;
  NoteLabel.WordWrap := True;
  NoteLabel.Width := NotePage.SurfaceWidth;
  NoteLabel.Caption :=
    'This installs the WGS Background Service, which lets Windows Game Server (WGS) keep managing '
    + 'your game servers even when you are logged out of Windows.'#13#10#13#10
    + 'IMPORTANT: install this into the SAME folder as your existing WGS installation (default: '
    + '"' + ExpandConstant('{autopf}') + '\WGS"). They share the same settings and server list — '
    + 'installing it somewhere else will not work.'#13#10#13#10
    + 'This registers and starts a real Windows Service ("' + '{#ServiceName}' + '") and requires '
    + 'administrator rights. After installing, open WGS -> Settings -> Service mode to point your '
    + 'WGS window at it (http://localhost:8766, with the access token shown in that window''s log '
    + 'on first run).';
end;
