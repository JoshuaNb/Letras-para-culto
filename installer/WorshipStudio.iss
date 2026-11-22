#ifndef AppSource
  #define AppSource "..\app\win-x64"
#endif
#ifndef AppVersion
  #define AppVersion "1.3.0"
#endif
#ifndef AppIdentity
  #define AppIdentity "{{7DE7E094-533F-4EB2-A817-CE015CE1A823}"
#endif
#ifndef PackageOutputDir
  #define PackageOutputDir "..\.."
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
MinVersion=10.0.22000
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

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Worship Studio"; Filename: "{app}\WorshipStudio.exe"
Name: "{group}\Desinstalar Worship Studio"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Worship Studio"; Filename: "{app}\WorshipStudio.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\WorshipStudio.exe"; Description: "Iniciar Worship Studio"; Flags: nowait postinstall skipifsilent
