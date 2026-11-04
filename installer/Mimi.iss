; Mimi installer (Inno Setup 6).
;
; D-02 was settled on this: a per-user install that needs no administrator, no
; certificate and no update service. See docs/engineering/PACKAGING.md for why
; Velopack and MSIX were rejected - in short, MSIX cannot be installed at all
; without a trusted signing certificate (D-07 is not done), and Velopack would
; take over the Start menu shortcut that the app itself owns and stamps with its
; AppUserModelID.
;
; Compile:
;   iscc /DAppVersion=<version> /DSourceDir=<publish folder> installer\Mimi.iss
;
; The version is never written in this file. CI passes it, so the installer, the
; executable and the release page cannot disagree.

#ifndef AppVersion
  #error AppVersion is required: compile with /DAppVersion=<version from Mimi.App.csproj>
#endif

#ifndef SourceDir
  #error SourceDir is required: compile with /DSourceDir=<published folder>
#endif

#define AppName "Mimi"
#define AppExe "Mimi.exe"
#define AppPublisher "Mimi project"
#define AppUrl "https://github.com/lxmtuu/Mimi"
#define AppUserModelId "Mimi.App"
#define AppProgId "Mimi.AudioFile"

[Setup]
; A stable identifier for "this is Mimi". Windows uses it to upgrade in place
; and to recognise the uninstaller. CHANGING IT MAKES EVERY EXISTING INSTALL A
; SECOND, PARALLEL COPY - the old one is then never replaced or removed.
AppId={{8B1F4C2A-6D3E-4A7B-9C15-2E4F6A8D0B93}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}

; Per-user: no elevation prompt, no administrator password, no UAC.
; The folder is deliberately not %LOCALAPPDATA%\Mimi - that is the user's
; library, settings and logs, and an installer must never own it.
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline

; Windows 10 1809 is the floor the app declares; the build is x64.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

OutputDir=..\dist
OutputBaseFilename=Mimi-{#AppVersion}-Setup
SetupIconFile=..\src\Mimi.App\Assets\mimi.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName} {#AppVersion}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes

; MIT requires the notice to travel with the binaries; TagLibSharp is LGPL-2.1
; and requires its credit and licence text. Both are copied into the published
; folder by CI and installed with the app.
LicenseFile=..\LICENSE
InfoBeforeFile=BEFORE-INSTALL.txt

; Upgrade in place: keep the folder the user chose last time, close a running
; Mimi so the files can be replaced, but do not restart it behind their back.
UsePreviousAppDir=yes
CloseApplications=yes
CloseApplicationsFilter={#AppExe}
RestartApplications=no

; The app registers itself per-user from Settings; the installer does not touch
; associations, so it must not claim it did.
ChangesAssociations=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
; Inno ships no Vietnamese translation, so the wizard is English. The app itself
; is bilingual; the installer is one screen of "Next".

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Everything the published build produced, including the runtime. TagLibSharp
; and the other managed libraries stay as separate .dll files on purpose: LGPL
; requires that the user can replace them (no single-file publish).
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; A previous version may have shipped a library that no longer exists. Deleting
; only what this installer owns (the program folder) keeps upgrades from leaving
; stale DLLs behind. The user data folder is a different path and is untouched.
Type: filesandordirs; Name: "{app}\*"

[Icons]
; Exactly the path the app itself would create: {userprograms}\Mimi.lnk. Because
; the target and the AppUserModelID match, the app's own identity repair sees it
; as already correct instead of adding a second entry.
Name: "{userprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExe}"; AppUserModelID: "{#AppUserModelId}"; Comment: "Mimi - music player"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExe}"; AppUserModelID: "{#AppUserModelId}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; There is deliberately no [UninstallDelete] entry: the uninstaller removes
; the files and shortcuts it installed, and nothing else. The user's library,
; settings and logs in %LOCALAPPDATA%\Mimi are theirs; the single prompt in
; [Code] is the only way they can leave, and it defaults to keeping them.

[Code]
const
  UserDataFolder = 'Mimi';
  { Where Windows looks for "which app handles these files". The app writes the
    value here itself (WindowsFileAssociationService.RegisteredApplicationsPath);
    this constant has to name the same key, or the cleanup below silently does
    nothing at all. }
  RegisteredApplicationsPath = 'Software\RegisteredApplications';

var
  RemoveUserData: Boolean;

function UserDataPath: String;
begin
  Result := ExpandConstant('{localappdata}\' + UserDataFolder);
end;

{ Is Path equal to, or inside, the user's data folder? }
function IsInsideUserData(const Path: String): Boolean;
begin
  Result :=
    (CompareText(Path, UserDataPath) = 0) or
    (Pos(Lowercase(UserDataPath) + '\', Lowercase(Path)) = 1);
end;

{ A folder is a Mimi install if it already holds Mimi.exe. }
function LooksLikeMimiInstall(const Path: String): Boolean;
begin
  Result := FileExists(AddBackslash(Path) + '{#AppExe}');
end;

function FolderHasAnything(const Path: String): Boolean;
var
  Found: TFindRec;
begin
  Result := False;
  if not DirExists(Path) then exit;

  if FindFirst(AddBackslash(Path) + '*', Found) then
  begin
    try
      repeat
        if (Found.Name <> '.') and (Found.Name <> '..') then
        begin
          Result := True;
          break;
        end;
      until not FindNext(Found);
    finally
      FindClose(Found);
    end;
  end;
end;

{ The one rule that prevents data loss: [InstallDelete] clears the program
  folder on upgrade, so pointing the installer at a folder that is not a Mimi
  install would empty it first. Returns an empty string when the target is
  fine, or the message to show when it is not. }
function TargetProblem(const Target: String): String;
begin
  Result := '';

  if IsInsideUserData(Target) then
  begin
    Result :=
      'Mimi cannot be installed inside its own data folder.' + #13#10#13#10 +
      'That folder holds your library, settings and logs, and this installer ' +
      'replaces the contents of the folder it installs into.' + #13#10#13#10 +
      'Choose another folder - the one suggested by default is fine.';
    exit;
  end;

  if FolderHasAnything(Target) and (not LooksLikeMimiInstall(Target)) then
  begin
    Result :=
      'That folder already contains something that is not a Mimi ' +
      'installation.' + #13#10#13#10 +
      'Installing here would replace its contents. Choose an empty folder, or ' +
      'the folder of an existing Mimi install to upgrade it.';
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Problem: String;
begin
  Result := True;
  if CurPageID <> wpSelectDir then exit;

  Problem := TargetProblem(WizardDirValue);
  if Problem <> '' then
  begin
    MsgBox(Problem, mbError, MB_OK);
    Result := False;
  end;
end;

{ /SILENT and /VERYSILENT never show the wizard, so the same check has to run
  again here - otherwise the guard would only protect people who click. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := TargetProblem(ExpandConstant('{app}'));
  if Result <> '' then
    Result := 'Mimi cannot be installed into that folder.' + #13#10#13#10 + Result;
end;

{ One extension, two places: HKCU\Software\Classes and Explorer's own record of
  "open with" choices. Both hold a value named after Mimi's ProgID. Pascal Script
  needs this before the call that uses it. }
procedure RemoveOpenWithProgid(const Extension: String);
begin
  RegDeleteValue(
    HKCU,
    'Software\Classes\' + Extension + '\OpenWithProgids',
    '{#AppProgId}');
  RegDeleteValue(
    HKCU,
    'Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\' +
      Extension + '\OpenWithProgids',
    '{#AppProgId}');
end;

{ Asks once, defaulting to "keep". Deleting a library is not what "uninstall"
  means to most people, so it is never the default and never silent. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Answer: Integer;
  Registered: String;
begin
  if CurUninstallStep <> usPostUninstall then exit;

  if not UninstallSilent then
  begin
    Answer := MsgBox(
      'Keep your Mimi library, settings and logs?' + #13#10#13#10 +
      'Yes - keep them (recommended). Installing Mimi again later finds your ' +
      'library where you left it.' + #13#10 +
      'No - also delete ' + UserDataPath + '. This cannot be undone.',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON1);
    RemoveUserData := (Answer = IDNO);
  end;

  { The app registers a ProgID and "Open with" entries in HKEY_CURRENT_USER when
    the user turns that on in Settings. Left behind, they advertise an executable
    that no longer exists. Only Mimi's own keys go, and the RegisteredApplications
    value only if it is the one Mimi wrote. }
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Mimi');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\{#AppProgId}');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Applications\{#AppExe}');
  RegDeleteKeyIncludingSubkeys(
    HKCU, 'Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppExe}');

  { "Open with" keeps one value per extension inside keys that other apps also
    use, so only Mimi's own value goes - never the key. }
  RemoveOpenWithProgid('.mp3');
  RemoveOpenWithProgid('.wav');
  RemoveOpenWithProgid('.aiff');
  RemoveOpenWithProgid('.aif');
  RemoveOpenWithProgid('.wma');
  RemoveOpenWithProgid('.m4a');
  RemoveOpenWithProgid('.aac');
  RemoveOpenWithProgid('.flac');

  { Only if this is still the value Mimi wrote: another player may have taken
    the name over since, and it is not ours to delete. }
  if RegQueryStringValue(
       HKCU, RegisteredApplicationsPath, '{#AppName}', Registered) and
     (CompareText(Registered, 'Software\Mimi\Capabilities') = 0) then
  begin
    RegDeleteValue(HKCU, RegisteredApplicationsPath, '{#AppName}');
  end;

  if RemoveUserData then
    DelTree(UserDataPath, True, True, True);
end;
