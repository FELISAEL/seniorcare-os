$ErrorActionPreference = 'Stop'

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$mobileProject = Join-Path $projectRoot 'src\Mobile\SeniorCare.Mobile\SeniorCare.Mobile.csproj'
$androidSdk = Join-Path $env:LOCALAPPDATA 'SeniorCare\AndroidSdk'
$javaSdk = Join-Path $env:LOCALAPPDATA 'SeniorCare\JavaSdk'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'No se encontró el SDK de .NET. Instale .NET 10 antes de continuar.'
}

Write-Host 'Preparando las herramientas de Android para SeniorCare...' -ForegroundColor Cyan
Write-Host "Android SDK: $androidSdk"
Write-Host "Java SDK:    $javaSdk"
Write-Host ''

New-Item -ItemType Directory -Path $androidSdk -Force | Out-Null
New-Item -ItemType Directory -Path $javaSdk -Force | Out-Null

dotnet workload restore $mobileProject
if ($LASTEXITCODE -ne 0) {
    throw 'No se pudo restaurar la carga de trabajo de .NET MAUI.'
}

dotnet build $mobileProject `
    -t:InstallAndroidDependencies `
    -f net10.0-android `
    "-p:AndroidSdkDirectory=$androidSdk" `
    "-p:JavaSdkDirectory=$javaSdk" `
    -p:AcceptAndroidSdkLicenses=True
if ($LASTEXITCODE -ne 0) {
    throw 'No se pudieron instalar las dependencias de Android.'
}

[Environment]::SetEnvironmentVariable('ANDROID_HOME', $androidSdk, 'User')
[Environment]::SetEnvironmentVariable('ANDROID_SDK_ROOT', $androidSdk, 'User')
[Environment]::SetEnvironmentVariable('JAVA_HOME', $javaSdk, 'User')

$env:ANDROID_HOME = $androidSdk
$env:ANDROID_SDK_ROOT = $androidSdk
$env:JAVA_HOME = $javaSdk

dotnet restore $mobileProject `
    "-p:AndroidSdkDirectory=$androidSdk" `
    "-p:JavaSdkDirectory=$javaSdk"
if ($LASTEXITCODE -ne 0) {
    throw 'No se pudieron restaurar los paquetes de la aplicación móvil.'
}

dotnet build $mobileProject `
    -f net10.0-android `
    --no-restore `
    "-p:AndroidSdkDirectory=$androidSdk" `
    "-p:JavaSdkDirectory=$javaSdk"
if ($LASTEXITCODE -ne 0) {
    throw 'Android quedó instalado, pero la aplicación móvil no pudo compilarse.'
}

Write-Host ''
Write-Host 'Android quedó configurado y la aplicación móvil compiló correctamente.' -ForegroundColor Green
Write-Host 'Cierre y vuelva a abrir VS Code para que detecte las nuevas variables.' -ForegroundColor Yellow
