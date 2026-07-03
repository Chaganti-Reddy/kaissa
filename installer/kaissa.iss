; Inno Setup script for the Kaissa Windows desktop build.
; Version is passed in from CI: ISCC /DMyAppVersion=<tag> installer\kaissa.iss
; Run from the repo root so the relative Source path resolves.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

#define MyAppName "Kaissa"
#define MyAppExeName "Kaissa.exe"
#define MyAppPublisher "Kaissa"
; Strip a leading "v" from the tag for the numeric version resource (e.g. v0.1.4 -> 0.1.4).
#define MyAppVersionNumeric StringChange(MyAppVersion, "v", "")

[Setup]
AppId={{B1E4B2A0-9C3D-4E77-9A1B-0F2C6D8E4A11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
VersionInfoVersion={#MyAppVersionNumeric}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
; On an existing install, skip the welcome/dir/ready pages so it reads as an in-place update
; instead of a fresh setup. First-time installs still show the directory page (auto).
DisableWelcomePage=yes
DisableDirPage=auto
DisableReadyPage=yes
; Close a running Kaissa during an upgrade instead of failing on locked files.
CloseApplications=yes
RestartApplications=no
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
; Create the desktop shortcut unconditionally so there is no "Select Additional Tasks" page — an
; upgrade then shows only the progress and finished pages, reading as an update rather than a setup.
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
