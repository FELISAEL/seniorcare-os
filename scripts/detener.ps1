$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun.ps1')

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $projectRoot

try {
    $project = Get-SeniorCareProject

    # Sin -v: los volúmenes (la base de datos) se conservan. Solo afecta a este proyecto.
    docker compose -p $project down
    if ($LASTEXITCODE -ne 0) {
        throw 'No se pudieron detener los servicios.'
    }

    Write-Host "SeniorCare OS ($project) se detuvo correctamente." -ForegroundColor Green
    Write-Host 'Los datos de desarrollo se conservaron.'
}
finally {
    Pop-Location
}
