; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "Caspar's Playout Client"
#define MyAppVersion "0.1.1.5"
#define MyAppMainVersion "0.1.1.0"
#define MyAppCompany "Sublan.tv"
#define MyAppPublisher "Christopher Diekkamp"
#define MyAppURL "http://github.com/SublanTV/CasparsPlayoutClient"
#define MyAppExeName "CasparsPlayoutClient.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{474CEBC5-64D8-4FEA-9D38-309E196A7A60}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=C:\Daten\Gemeinde\Sublan\Programierung\CasparsPlayoutClient\LICENSE.txt
OutputDir=C:\Daten\Gemeinde\Sublan\Programierung\CasparsPlayoutClient\Setup
OutputBaseFilename=setup
Compression=lzma
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 0,6.1

[Files]
Source: "C:\Daten\Gemeinde\Sublan\Programierung\CasparsPlayoutClient\CasparsPlayoutClient\bin\Release\CasparsPlayoutClient.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Daten\Gemeinde\Sublan\Programierung\CasparsPlayoutClient\CasparsPlayoutClient\bin\Release\Bespoke.Common.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Daten\Gemeinde\Sublan\Programierung\CasparsPlayoutClient\CasparsPlayoutClient\bin\Release\Bespoke.Common.Osc.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Daten\Gemeinde\Sublan\Programierung\CasparsPlayoutClient\CasparsPlayoutClient\bin\Release\CasparCGNETConnector.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Daten\Gemeinde\Sublan\Programierung\CasparsPlayoutClient\CasparsPlayoutClient\bin\Release\logger.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Daten\Gemeinde\Sublan\Programierung\CasparsPlayoutClient\CasparsPlayoutClient\bin\Release\img\*"; DestDir: "{app}\img\"; Flags: ignoreversion
Source: "C:\Daten\Gemeinde\Sublan\Programierung\CasparsPlayoutClient\CasparsPlayoutClient\bin\Release\CasparsPlayoutClient.exe.config"; DestDir: "{app}"; Flags: ignoreversion
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Dirs]
; logfolder in users appdata
Name: "{userappdata}\{#MyAppCompany}\{#MyAppName}\{#MyAppMainVersion}\log"
Name: "{userappdata}\{#MyAppCompany}\{#MyAppName}\{#MyAppMainVersion}\library"
Name: "{userappdata}\{#MyAppCompany}\{#MyAppName}\{#MyAppMainVersion}\playlist"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

