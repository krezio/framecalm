; Framecalm installer (Inno Setup 6). Per-user install, no admin prompt.
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

[Setup]
AppId={{9C3E5B1A-4F2D-4E8B-A7C6-1D0F5E2B8A93}
AppName=Framecalm
AppVersion={#AppVersion}
AppPublisher=krezio
AppPublisherURL=https://framecalm.krezio.xyz
AppSupportURL=https://github.com/krezio/framecalm/issues
DefaultDirName={localappdata}\Programs\Framecalm
DefaultGroupName=Framecalm
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=Framecalm-Setup
SetupIconFile=..\src\Framecalm.App\Assets\framecalm.ico
UninstallDisplayIcon={app}\Framecalm.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\dist\publish\Framecalm.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\publish\Framecalm.ElevatedHelper.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\publish\PresentMon.exe"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\dist\publish\PresentMon-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\Framecalm"; Filename: "{app}\Framecalm.exe"
Name: "{autodesktop}\Framecalm"; Filename: "{app}\Framecalm.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Framecalm.exe"; Description: "Launch Framecalm"; Flags: nowait postinstall skipifsilent
; after an in-app update (a silent install started by Framecalm itself), open the new version
Filename: "{app}\Framecalm.exe"; Flags: nowait; Check: IsAppUpdate

[UninstallRun]
; puts every change Framecalm made back before it's removed
Filename: "{app}\Framecalm.exe"; Parameters: "--undo-all-quiet"; Flags: runhidden waituntilterminated skipifdoesntexist; RunOnceId: "UndoAll"

[Code]
function IsAppUpdate: Boolean;
begin
  Result := ExpandConstant('{param:relaunch|0}') = '1';
end;
