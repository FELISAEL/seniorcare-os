$ErrorActionPreference = 'Stop'

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $projectRoot

try {
    docker compose down
    if ($LASTEXITCODE -ne 0) {
        throw 'No se pudieron detener los servicios.'
    }

    Write-Host 'SeniorCare OS se detuvo correctamente.' -ForegroundColor Green
    Write-Host 'Los datos de desarrollo se conservaron.'
}
finally {
    Pop-Location
}

