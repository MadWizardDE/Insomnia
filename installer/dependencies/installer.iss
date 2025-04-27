#include "CodeDependencies.iss"

[Code]

const
  NPCAP_VERSION = '1.81';
  
function IsNpcapInstalled(): Boolean;
begin
  Result := RegKeyExists(GetHKLM, 'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\NpcapInst');   
end;

function IsDuoInstalled(): Boolean;
begin
  Result := RegKeyExists(GetHKLM, 'SOFTWARE\Duo');   
end;

function IsBridgeReady(): Boolean;
var
  Ready: String;
begin
  #ifdef DisableBridge
    Result := False;
  #else
    Result := True;
  #endif
end;

procedure Dependency_Clear;
begin
  SetLength(Dependency_Memo, 0)
  SetArrayLength(Dependency_List, 0);
end;

procedure Dependency_Npcap;
begin
  if not IsNpcapInstalled() then
    Dependency_Add('npcap-'+NPCAP_VERSION+'.exe', '', 'Npcap', 'https://npcap.com/dist/npcap-' + NPCAP_VERSION + '.exe', '', False, False);
end;

<event('NextButtonClick')>
function CheckDependencies(PageID: Integer): Boolean;
begin
  if PageID = wpSelectComponents then
  begin
    Dependency_Clear;
    Dependency_AddVC2015To2022;
    Dependency_AddDotNet80Desktop;

    if IsComponentSelected('InsomniaService\NetworkMonitor') then
      Dependency_Npcap;
    if IsComponentSelected('plugins\InsomniaServiceBridge') then
      Dependency_AddDotNet48;
  end;

  Result := True;
end;

<event('PrepareToInstall')>
function Npcap_PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  NeedsRestart := False;
  
  // idpAddFileComp('https://npcap.com/dist/npcap-1.81.exe',  ExpandConstant('{tmp}\npcap.exe'),  'insomniaservice\networkmonitor');
  // idpDownloadAfter(wpReady);
  
  if FileExists(ExpandConstant('{tmp}\npcap.exe')) then
  begin
    if not Exec(ExpandConstant('{tmp}\npcap.exe'), '/quiet', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
      MsgBox('Failed to run the dependency installer. Error code: ' + IntToStr(ResultCode), mbError, MB_OK);
  end;
end;

<event('CurPageChanged')>
procedure CheckPrerequisites(CurPageID: Integer);
begin
  if CurPageID = wpSelectComponents then
  begin
    //WizardForm.ComponentsList.Items[0].Enabled := False;

    //Log(WizardForm.ComponentsList.Items[0].SubItems[0]);
    
    //Log(WizardForm.ComponentsList.ItemSubitem[1]);
    
    //WizardForm.ComponentsList.ItemEnabled[1] := False;
  end;
end;
