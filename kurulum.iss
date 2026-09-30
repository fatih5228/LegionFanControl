; Legion Y520 Fan Kontrol — Inno Setup kurulum betigi
; Derlemek icin: ISCC.exe kurulum.iss   (Inno Setup 6 gereklidir)
; Cikti: dist\LegionFanControl-Setup-v<surum>.exe
; Surum, derlenmis LegionFanControl.exe dosyasindan okunur (Program.cs: CurrentVersion).

#define MyAppName "Legion Y520 Fan Kontrol"
#define MyAppVersion GetStringFileInfo("LegionFanControl.exe", "ProductVersion")
#define MyAppPublisher "fatih5228"
#define MyAppURL "https://github.com/fatih5228/LegionFanControl"
#define MyAppExeName "LegionFanControl.exe"

[Setup]
AppId={{7C4E2B1A-9F3D-4E5A-B8C6-1D2E3F4A5B6C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\LegionFanControl
DefaultGroupName={#MyAppName}
PrivilegesRequired=admin
OutputDir=dist
OutputBaseFilename=LegionFanControl-Setup-v{#MyAppVersion}
SetupIconFile=app.ico
UninstallDisplayIcon={app}\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstü kısayolu oluştur"; GroupDescription: "Ek kısayollar:"
Name: "startup"; Description: "Windows açılışında otomatik başlat (yönetici yetkisiyle)"; GroupDescription: "Başlangıç:"

[Files]
Source: "LegionFanControl.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "LibreHardwareMonitorLib.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "HidSharp.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "System.Management.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "app.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "app_off.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "KULLANIM.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\Kullanım Kılavuzu"; Filename: "{app}\KULLANIM.txt"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
; "Windows ile başlat" secildiyse Görev Zamanlayici'ya gorev ekle.
; Gorevi uygulamanin kendisi XML ile kaydeder (pilde de baslar, sure siniri yoktur).
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-startup"; Flags: runhidden waituntilterminated; Tasks: startup
Filename: "{app}\{#MyAppExeName}"; Description: "Uygulamayı şimdi çalıştır"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""LegionFanControl"" /F"; Flags: runhidden; RunOnceId: "RemoveStartupTask"
