; TaskIsland installer (Inno Setup 6, PLAN.md Phase 7 step 4). Per-user install, no administrator rights, no UAC prompt.
; Build: powershell -File tools\build-installer.ps1   (reads the version from publish\win-x64\TaskIsland.exe)
;
; The defines below default to the release values. tools\ui-tests\installer-test.ps1 overrides them to build a TEST
; installer that uses its own app id, folder, data folder, registry key and app lock, so the install / upgrade / uninstall
; tests never touch a real TaskIsland on the same PC. The [Setup], [Files], [Icons] and [Code] sections are the same for both.

#ifndef AppVersion
  #define AppVersion "1.0.3"
#endif
#ifndef NumVersion
  #define NumVersion "1.0.3.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\win-x64"
#endif
#ifndef AppIdGuid
  #define AppIdGuid "39F89B18-992E-4E46-8881-1E8CCC6F1D16"
#endif
#ifndef AppTitle
  #define AppTitle "TaskIsland"
#endif
#ifndef AppMutexName
  #define AppMutexName "Local\TaskIsland-7F3A9C2E-SingleInstance"
#endif
#ifndef RunKey
  #define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"
#endif
#ifndef DataDir
  #define DataDir "{localappdata}\TaskIsland"
#endif
#ifndef OutName
  #define OutName "TaskIsland-Setup-" + AppVersion + "-x64"
#endif

[Setup]
; AppId never changes: upgrades install over the top of the same entry.
AppId={{{#AppIdGuid}}
AppName={#AppTitle}
AppVersion={#AppVersion}
AppVerName={#AppTitle} {#AppVersion}
AppPublisher=RieSources
AppPublisherURL=https://rie-sources.com
AppSupportURL=https://github.com/Riekie3/Dynamic-Island-To-Do-List
AppCopyright=Copyright (C) 2026 RieSources
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#AppTitle}
DisableProgramGroupPage=yes
DisableReadyPage=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
; A running TaskIsland holds this lock: Setup and the uninstaller ask the user to close it first.
AppMutex={#AppMutexName}
CloseApplications=yes
RestartApplications=no
SetupIconFile=..\src\TaskIsland.App\Assets\TaskIsland.ico
UninstallDisplayIcon={app}\TaskIsland.exe
UninstallDisplayName={#AppTitle}
WizardStyle=modern
ShowLanguageDialog=no
OutputDir=Output
OutputBaseFilename={#OutName}
Compression=lzma2
SolidCompression=yes
VersionInfoCompany=RieSources
VersionInfoDescription=TaskIsland Setup
VersionInfoVersion={#NumVersion}
VersionInfoTextVersion={#AppVersion}
VersionInfoProductName=TaskIsland
VersionInfoProductVersion={#NumVersion}
VersionInfoProductTextVersion={#AppVersion}
VersionInfoCopyright=Copyright (C) 2026 RieSources

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The portable README.txt describes the zip, not an installed copy, so it is left out.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "README.txt"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppTitle}"; Filename: "{app}\TaskIsland.exe"; Comment: "Dynamic Island style to-do list"
Name: "{autodesktop}\{#AppTitle}"; Filename: "{app}\TaskIsland.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\TaskIsland.exe"; Description: "Launch TaskIsland"; Flags: nowait postinstall skipifsilent

[Code]
// "Start with Windows" is owned by the app (Settings > General, on by default): it writes the HKCU Run value on its first
// launch and removes it when switched off. Setup therefore writes no Run value (an upgrade would otherwise switch it back
// on). The uninstaller removes the value, but only when it points into this install folder, so a portable copy's entry stays.
procedure RemoveRunValue();
var
  Value, AppDir: String;
begin
  if RegQueryStringValue(HKCU, '{#RunKey}', 'TaskIsland', Value) then
  begin
    AppDir := Lowercase(AddBackslash(ExpandConstant('{app}')));
    if Pos(AppDir, Lowercase(Value)) > 0 then
    begin
      RegDeleteValue(HKCU, '{#RunKey}', 'TaskIsland');
      Log('Removed the Start with Windows entry: ' + Value);
    end
    else
      Log('Start with Windows entry left alone (points elsewhere): ' + Value);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usUninstall then
    RemoveRunValue();
  if CurUninstallStep = usPostUninstall then
  begin
    // Tasks, settings, backups and logs live outside the program folder. Kept unless the user says Yes (default No;
    // a silent uninstall keeps them too).
    DataDir := ExpandConstant('{#DataDir}');
    if DirExists(DataDir) then
    begin
      if SuppressibleMsgBox('Also delete your tasks and backups?' + #13#10#13#10 + DataDir + #13#10#13#10 +
        'Choose No to keep them. Installing TaskIsland again later finds them.',
        mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES then
      begin
        if DelTree(DataDir, True, True, True) then
          Log('Deleted the data folder: ' + DataDir)
        else
          Log('Could not delete all of the data folder: ' + DataDir);
      end
      else
        Log('Data folder kept: ' + DataDir);
    end;
  end;
end;
