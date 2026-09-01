$ErrorActionPreference = 'Stop'

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

    if (-not (Test-Path '.env')) {
        Copy-Item '.env.example' '.env'
        Write-Host 'Se creó la configuración local .env.' -ForegroundColor Cyan
    }

    Write-Host 'Construyendo e iniciando SeniorCare OS...' -ForegroundColor Cyan
    docker compose up --build --detach
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker no pudo iniciar los servicios.'
    }

    $healthUrls = @(
        'http://localhost:7001/health',
        'http://localhost:7002/health',
        'http://localhost:7003/health',
        'http://localhost:7004/health',
        'http://localhost:7005/health'
    )

    $deadline = (Get-Date).AddMinutes(3)
    do {
        $ready = $true
        foreach ($url in $healthUrls) {
            try { Invoke-RestMethod -Uri $url -TimeoutSec 4 | Out-Null }
            catch { $ready = $false; break }
        }
        if (-not $ready) { Start-Sleep -Seconds 4 }
    } until ($ready -or (Get-Date) -ge $deadline)

    Write-Host ''
    if ($ready) { Write-Host 'SeniorCare OS está listo.' -ForegroundColor Green }
    else { Write-Host 'Los contenedores iniciaron; revisá docker compose ps si una API continúa inicializando.' -ForegroundColor Yellow }

    Write-Host ''
    Write-Host 'Portal público + login: http://localhost:8088'
    Write-Host 'Monitoreo:             http://localhost:18888'
    Write-Host ''
    Write-Host 'Administrador: admin / Cambiar123!'
    Write-Host 'Cuidador:      cuidador / Cuidador123!'
    Write-Host 'Adulto mayor:  maria / Maria123!'
    Write-Host 'Familiar:      ana / Familia123!'
}
finally {
    Pop-Location
}
