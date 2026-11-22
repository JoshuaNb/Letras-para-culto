#ifndef AppSource
  #define AppSource "..\app\win-x64"
#endif
#ifndef LegacyAppSource
  #define LegacyAppSource "..\app\win-legacy-x64"
#endif
#ifndef AppVersion
  #define AppVersion "1.6.0"
#endif
#ifndef AppIdentity
  #define AppIdentity "{{7DE7E094-533F-4EB2-A817-CE015CE1A823}"
#endif
#ifndef PackageOutputDir
  #define PackageOutputDir "..\.."
#endif
#ifndef RuntimeCleanupFile
  #define RuntimeCleanupFile "runtime-cleanup.iss"
#endif

[Setup]
AppId={#AppIdentity}
AppName=Worship Studio
AppVersion={#AppVersion}
AppPublisher=Worship Studio
AppComments=Operação e projeção de letras de louvores offline
DefaultDirName={localappdata}\Programs\Worship Studio
DefaultGroupName=Worship Studio
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=6.1sp1
OutputDir={#PackageOutputDir}
OutputBaseFilename=WorshipStudio-Setup-{#AppVersion}
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\WorshipStudio.exe
LicenseFile=..\LICENSE.txt
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#AppVersion}.0

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[InstallDelete]
#include RuntimeCleanupFile

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: UseModernRuntime
Source: "{#LegacyAppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: UseLegacyRuntime

[Icons]
Name: "{group}\Worship Studio"; Filename: "{app}\WorshipStudio.exe"
Name: "{group}\Desinstalar Worship Studio"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Worship Studio"; Filename: "{app}\WorshipStudio.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\WorshipStudio.exe"; Description: "Iniciar Worship Studio"; Flags: nowait postinstall skipifsilent

[Code]
function GetModuleHandle(ModuleName: string): LongWord;
  external 'GetModuleHandleW@kernel32.dll stdcall';
function GetProcAddress(Module: LongWord; ProcName: AnsiString): LongWord;
  external 'GetProcAddress@kernel32.dll stdcall';

function UseLegacyRuntime: Boolean;
var
  Version: TWindowsVersion;
begin
  // The override exists only in a separately compiled QA installer, never as an end-user switch.
#ifdef ForceLegacyQA
  Result := True;
#else
  GetWindowsVersionEx(Version);
  Result := (Version.Major < 10) or ((Version.Major = 10) and (Version.Build < 19045));
#endif
end;

function UseModernRuntime: Boolean;
begin
  Result := not UseLegacyRuntime;
end;

function InitializeSetup: Boolean;
var
  Version: TWindowsVersion;
  Missing: string;
begin
  Result := True;
  GetWindowsVersionEx(Version);
  if UseLegacyRuntime then Log('Runtime selecionado: .NET 6 legado')
  else Log('Runtime selecionado: .NET 10 moderno');
  if (Version.Major = 6) and (Version.Minor = 1) then
  begin
    Missing := '';
    if GetProcAddress(GetModuleHandle('kernel32.dll'), 'AddDllDirectory') = 0 then
      Missing := Missing + #13#10 + '- Atualização KB3063858 (ou atualização cumulativa equivalente).';
    if Missing <> '' then
    begin
      SuppressibleMsgBox('Este Windows 7 SP1 precisa dos componentes abaixo antes de instalar o Worship Studio:' + Missing + #13#10#13#10 + 'Instale os pacotes oficiais da Microsoft, reinicie o computador se solicitado e execute este instalador novamente. Nenhum download será iniciado pelo programa.', mbCriticalError, MB_OK, IDOK);
      Result := False;
    end;
  end;
end;
