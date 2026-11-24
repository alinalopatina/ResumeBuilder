# Сборка дистрибутива ResumeBuilder (лаба 10.1).
# Запуск из корня репозитория:
#   powershell -ExecutionPolicy Bypass -File build\publish.ps1
#   powershell -ExecutionPolicy Bypass -File build\publish.ps1 -Version 1.2.1
# Результат в папке dist\:
#   ResumeBuilder-<версия>-portable-win-x64.zip — портативная версия (распаковать и запустить)
#   ResumeBuilder-Setup-<версия>.exe            — установщик (если установлен Inno Setup 6)
param(
    [string]$Version = ""
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "publish\win-x64"
$distDir = Join-Path $root "dist"

Write-Host "1. Публикация self-contained (один exe со средой .NET внутри)" -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
$publishArgs = @(
    "publish", "$root\src\ResumeBuilder.Wpf\ResumeBuilder.Wpf.csproj",
    "-c", "Release", "-r", "win-x64", "--self-contained", "true",
    "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:DebugType=None",
    "-o", $publishDir
)
if ($Version) { $publishArgs += "-p:Version=$Version" }
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish завершился с ошибкой" }

# Версия — из собранного exe (единый источник — Directory.Build.props или параметр -Version)
$fileVersion = [version](Get-Item "$publishDir\ResumeBuilder.exe").VersionInfo.FileVersion
$ver = "{0}.{1}.{2}" -f $fileVersion.Major, $fileVersion.Minor, $fileVersion.Build
Write-Host "   версия $ver"

New-Item -ItemType Directory -Force $distDir | Out-Null

Write-Host "2. Портативная версия (zip)" -ForegroundColor Cyan
$zip = Join-Path $distDir "ResumeBuilder-$ver-portable-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path "$publishDir\*" -DestinationPath $zip
Write-Host "   $zip"

Write-Host "3. Установщик (Inno Setup)" -ForegroundColor Cyan
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    & $iscc "/DMyAppVersion=$ver" "/DPublishDir=$publishDir" "/O$distDir" "$root\installer\ResumeBuilder.iss"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup завершился с ошибкой" }
} else {
    Write-Warning "Inno Setup 6 не найден — установщик не собран. Скачать: https://jrsoftware.org/isdl.php"
}

Write-Host "Готово: $distDir" -ForegroundColor Green
Get-ChildItem $distDir | Format-Table Name, Length
