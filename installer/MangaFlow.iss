; ============================================================================
; MangaFlow — Inno Setup installer script
;
; Packages the self-contained, unpackaged WinUI 3 publish output into a single
; setup .exe (Start Menu shortcut, uninstaller, optional desktop icon).
;
; Build steps:
;   1. Publish the app (Release, win-x64, self-contained):
;        dotnet publish MangaFlow.App/MangaFlow.App.csproj -c Release -r win-x64 ^
;          --self-contained true -p:PublishProfile=win-x64
;   2. Compile this script with Inno Setup 6:
;        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\MangaFlow.iss
;   (Or just run installer\build-installer.ps1 which does both.)
;
; Output: installer\Output\MangaFlow-Setup-<version>.exe
; ============================================================================

#define MyAppName "MangaFlow"
; MyAppVersion may be passed on the ISCC command line via /DMyAppVersion=x.y.z
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "MangaFlow"
#define MyAppExeName "MangaFlow.App.exe"

; Path to the publish folder, relative to this .iss file.
#define PublishDir "..\MangaFlow.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish"

[Setup]
AppId={{A7E3F1C9-4B2D-4E8A-9F1C-MANGAFLOW0001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
; The app is self-contained x64 — only allow install on 64-bit Windows.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
OutputDir=Output
OutputBaseFilename=MangaFlow-Setup-{#MyAppVersion}
; ~900 MB of payload — installs per-machine, so request elevation.
PrivilegesRequired=admin
DisableProgramGroupPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Recursively pack the entire publish folder (exe, DLLs, both native backends,
; WindowsAppSDK self-contained runtime, OCR models, etc.).
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
