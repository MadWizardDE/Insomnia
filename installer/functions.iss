[Code]

// --- Wizard Helper Functions --- //
const
  MF_BYCOMMAND = $00000000;
  MF_BYPOSITION = $00000400;

type
  HMENU = THandle;

function GetSystemMenu(hWnd: HWND; bRevert: BOOL): HMENU; external 'GetSystemMenu@user32.dll stdcall';
function DeleteMenu(hMenu: HMENU; uPosition, uFlags: UINT): BOOL; external 'DeleteMenu@user32.dll stdcall';
function GetMenuItemCount(hMenu: HMENU): Integer; external 'GetMenuItemCount@user32.dll stdcall';

procedure RemoveAboutMenu();
var
  SystemMenu: HMENU;
begin
  // get the menu handle
  SystemMenu := GetSystemMenu(WizardForm.Handle, False);
  // delete the `About Setup` menu (which has ID 9999)
  DeleteMenu(SystemMenu, 9999, MF_BYCOMMAND);
  // delete the separator
  DeleteMenu(SystemMenu, GetMenuItemCount(SystemMenu)-1, MF_BYPOSITION);
end;

type
  HICON = THandle;

  

const
  MAX_ICONS = 1;

function ExtractIconEx(lpFile: String; nIconIndex: Integer; var phiconLarge, phiconSmall: Integer; nIcons: Integer): Integer;
external 'ExtractIconExA@shell32.dll stdcall';



  
// +++ Registry Helper Functions +++ //

var
  IsReinstall: Boolean;

function GetHKLM: Integer;
begin
  if IsWin64 then
    Result := HKLM64
  else
    Result := HKLM32;
end;


const
  UninstallKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}_is1';
  UninstallStringName = 'UninstallString';

procedure AddUninstallerArguments(Arguments: String);
var
  S: string;
begin
  if RegQueryStringValue(HKEY_LOCAL_MACHINE, UninstallKey, UninstallStringName, S) then
  begin
    S := S + ' ' + Arguments;
    
    if not RegWriteStringValue(HKEY_LOCAL_MACHINE, UninstallKey, UninstallStringName, S) then
      MsgBox('Error adding arguments to uninstaller.', mbError, MB_OK);
  end else
      MsgBox('Error reading arguments of uninstaller from ' + UninstallKey, mbError, MB_OK);
end;

function HasExistingConfig(): Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\config\config.xml'));
end;

function ReadExistingConfig(): Boolean;
var
  Command: String;
  Arguments: String;
  ResultCode: Integer;
begin
  Result := False;

  ExtractTemporaryFile('prefs.ini');
  
  Command := ExpandConstant('{app}\InsomniaService.exe');
  Arguments := ExpandConstant('config read "{tmp}\prefs.ini"')
  
  Log('Reading config: ' + Command + ' ' + Arguments);

  if not Exec(Command, Arguments, ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    MsgBox('Failed to read config.', mbError, MB_OK)
  else
    Result := True;
end;

function ShouldConfigureInsomnia(): Boolean;
begin
  Result := IsReinstall or not HasExistingConfig();
end;

