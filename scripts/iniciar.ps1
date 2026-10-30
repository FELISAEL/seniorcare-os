param(
    # Solo comprueba proyecto, configuración y secretos; no construye ni inicia nada.
    [switch]$SoloVerificar
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun.ps1')

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $projectRoot

try {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'Docker Desktop no está instalado o no se encuentra en PATH.'
    }

    docker info *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker Desktop está instalado, pero no está iniciado.'
    }

    $project = Get-SeniorCareProject
    Write-Host "Proyecto Docker: $project" -ForegroundColor Cyan

    # El sistema anterior usa los mismos puertos. No se toca: solo se avisa y se detiene el inicio.
    $legacyRunning = docker ps --quiet --filter "label=com.docker.compose.project=$($script:LegacyProject)"
    if ($legacyRunning) {
        throw "El sistema anterior ('$($script:LegacyProject)') está en ejecución y usa los mismos puertos. Este script no lo detiene; detenelo desde su propia instalación y volvé a intentar."
    }

    if ($SoloVerificar) {
        if (-not (Test-Path '.env')) { throw 'Falta .env: ejecutá scripts/configurar-entorno.ps1.' }
        $insecure = Get-InsecureSecretKeys (Read-EnvLines (Join-Path $projectRoot '.env'))
        if ($insecure.Count -gt 0) { throw ("Secretos de ejemplo o publicados en .env: " + ($insecure -join ', ')) }
        docker compose -p $project config --quiet
        if ($LASTEXITCODE -ne 0) { throw 'La configuración de Docker Compose no es válida.' }
        Write-Host "Verificación correcta: se iniciaría el proyecto '$project' con secretos propios." -ForegroundColor Green
        return
    }

    & (Join-Path $PSScriptRoot 'configurar-entorno.ps1') -Directorio $projectRoot -Proyecto $project

    Write-Host 'Construyendo e iniciando SeniorCare OS...' -ForegroundColor Cyan
    docker compose -p $project up --build --detach
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker no pudo iniciar los servicios.'
    }

    $healthUrl = 'http://localhost:7000/health'

    $deadline = (Get-Date).AddMinutes(3)
    do {
        $ready = $true
        try {
            $health = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 4
            if ($health.status -ne 'healthy') { $ready = $false }
        }
        catch { $ready = $false }
        if (-not $ready) { Start-Sleep -Seconds 4 }
    } until ($ready -or (Get-Date) -ge $deadline)

    Write-Host ''
    if ($ready) {
        Write-Host 'SeniorCare OS está listo.' -ForegroundColor Green
    }
    else {
        Write-Host "Los contenedores iniciaron; revisá docker compose -p $project ps si la API continúa inicializando (docker compose -p $project logs seniorcare-api)." -ForegroundColor Yellow
        exit 1
    }

    Write-Host ''
    Write-Host 'Portal público + login: http://localhost:8088'
    Write-Host 'Monitoreo:             http://localhost:18888'
    Write-Host ''
    Write-Host 'Usuarios de demostración: admin, cuidador, maria, ana'
    Write-Host 'Contraseñas: variables SEED_*_PASSWORD del archivo .env local.'
}
finally {
    Pop-Location
}
