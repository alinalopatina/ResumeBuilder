; Установщик ResumeBuilder (лаба 10.1, модуль 2). Inno Setup 6: https://jrsoftware.org/isdl.php
; Собирается скриптом build\publish.ps1 или вручную: открыть этот файл в Inno Setup → Build → Compile.
; Перед сборкой нужна публикация в publish\win-x64 (см. build\publish.ps1).

#define MyAppName "ResumeBuilder"
#define MyAppExeName "ResumeBuilder.exe"
#define MyAppPublisher "Лопатина А. Н."
#define MyAppURL "https://github.com/alinalopatina/ResumeBuilder"

#ifndef PublishDir
  #define PublishDir AddBackslash(SourcePath) + "..\publish\win-x64"
#endif

; Версия — из собранного exe (единый источник истины — Directory.Build.props),
; либо передаётся из скрипта: ISCC /DMyAppVersion=1.2.0
#ifndef MyAppVersion
  #define MyAppVersion GetVersionNumbersString(PublishDir + "\" + MyAppExeName)
#endif

[Setup]
; AppId одинаковый во всех версиях — новая версия ставится ПОВЕРХ старой, а не рядом
AppId={{8F3C2A71-5B4D-4E2A-9C1F-6A7D2B9E4C10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
VersionInfoVersion={#MyAppVersion}
; Установка для текущего пользователя без прав администратора (можно выбрать «для всех»)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=ResumeBuilder-Setup-{#MyAppVersion}
SetupIconFile=..\src\ResumeBuilder.Wpf\Resources\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; x64compatible — x64 Windows и Windows на ARM (через эмуляцию x64)
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Закрыть запущенную программу перед обновлением файлов
CloseApplications=yes
; Сообщить Проводнику, что изменились ассоциации файлов
ChangesAssociations=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "associate"; Description: "Открывать файлы резюме (*.rbin) в ResumeBuilder"; GroupDescription: "Ассоциации файлов:"

[Files]
; Всё содержимое публикации: ResumeBuilder.exe (внутри .NET и библиотеки) + appsettings*.json
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Ярлыки в меню «Пуск» и на рабочем столе
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Ассоциация расширения .rbin: двойной щелчок по файлу открывает его в программе.
; HKA = HKCU при установке для себя, HKLM — для всех пользователей
Root: HKA; Subkey: "Software\Classes\.rbin"; ValueType: string; ValueName: ""; ValueData: "ResumeBuilder.rbin"; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Classes\.rbin\OpenWithProgids"; ValueType: string; ValueName: "ResumeBuilder.rbin"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Classes\ResumeBuilder.rbin"; ValueType: string; ValueName: ""; ValueData: "Резюме ResumeBuilder"; Flags: uninsdeletekey; Tasks: associate
Root: HKA; Subkey: "Software\Classes\ResumeBuilder.rbin\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\ResumeBuilder.rbin\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: associate

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
