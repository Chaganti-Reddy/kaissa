; Inno Setup script for the Kaissa Windows desktop build.
; Version is passed in from CI: ISCC /DMyAppVersion=<tag> installer\kaissa.iss
; Run from the repo root so the relative Source path resolves.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

#define MyAppName "Kaissa"
#define MyAppExeName "Kaissa.exe"
#define MyAppPublisher "Kaissa"

[Setup]
AppId={{B1E4B2A0-9C3D-4E77-9A1B-0F2C6D8E4A11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Paths below are relative to this: the repo root (parent of installer\).
SourceDir=..
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=installer\kaissa.ico
OutputDir=installer-out
OutputBaseFilename=Kaissa-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Files]
Source: "build\Kaissa\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
