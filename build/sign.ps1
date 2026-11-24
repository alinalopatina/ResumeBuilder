# Подписывание кода тестовым сертификатом (лаба 10.1, задание 3.1).
#   powershell -ExecutionPolicy Bypass -File build\sign.ps1 -File dist\ResumeBuilder-Setup-1.2.0.exe
#   powershell -ExecutionPolicy Bypass -File build\sign.ps1 -File publish\win-x64\ResumeBuilder.exe -Trust
# -Trust — добавить тестовый сертификат в доверенные корневые (только на своём компьютере, для проверки).
# В реальном проекте сертификат покупают у удостоверяющего центра (DigiCert, Sectigo), и Windows
# доверяет ему сразу. Тестовый (самоподписанный) сертификат доверенным не является.
param(
    [Parameter(Mandatory = $true)][string]$File,
    [switch]$Trust
)
$ErrorActionPreference = "Stop"
$subject = "CN=ResumeBuilder Test Code Signing"

# 1. Сертификат: ищем созданный раньше или создаём новый (закрытый ключ остаётся в хранилище Windows)
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object Subject -eq $subject | Select-Object -First 1
if (-not $cert) {
    Write-Host "Создаём тестовый сертификат подписи кода..." -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject `
        -CertStoreLocation Cert:\CurrentUser\My -KeyAlgorithm RSA -KeyLength 3072 -NotAfter (Get-Date).AddYears(2)
}
Write-Host "Сертификат: $($cert.Subject), отпечаток $($cert.Thumbprint)"

# Открытая часть сертификата — в файл (её можно раздать для проверки подписи)
$cer = Join-Path $PSScriptRoot "ResumeBuilder-test.cer"
Export-Certificate -Cert $cert -FilePath $cer | Out-Null

if ($Trust) {
    # Windows спросит подтверждение
    Import-Certificate -FilePath $cer -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
    Write-Host "Сертификат добавлен в доверенные корневые (текущий пользователь)" -ForegroundColor Yellow
}

# 2. Подпись: хэш файла шифруется закрытым ключом, подпись и сертификат добавляются в файл.
#    Метка времени подтверждает, что подпись сделана, пока сертификат был действителен
$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if ($signtool) {
    & $signtool.FullName sign /sha1 $cert.Thumbprint /s My /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $File
    & $signtool.FullName verify /pa /v $File
} else {
    # signtool входит в Windows SDK; без него используем встроенную команду PowerShell
    Set-AuthenticodeSignature -FilePath $File -Certificate $cert -HashAlgorithm SHA256 `
        -TimestampServer "http://timestamp.digicert.com" | Out-Null
}

# 3. Проверка подписи
$sig = Get-AuthenticodeSignature $File
Write-Host "Статус подписи: $($sig.Status) — $($sig.StatusMessage)"
Write-Host "Подписал: $($sig.SignerCertificate.Subject)"
Write-Host "Свойства файла → вкладка «Цифровые подписи» покажет подпись"
