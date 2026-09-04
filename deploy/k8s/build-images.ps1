$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Push-Location $root

try {
    $images = @(
        @{ Name = 'api'; Project = 'api/SeniorCare.Api.csproj'; Dll = 'SeniorCare.Api.dll' },
        @{ Name = 'etl-worker'; Project = 'src/Workers/SeniorCare.Etl.Worker/SeniorCare.Etl.Worker.csproj'; Dll = 'SeniorCare.Etl.Worker.dll' }
    )

    foreach ($image in $images) {
        docker build `
            --file infra/docker/DotNetService.Dockerfile `
            --build-arg "PROJECT_PATH=$($image.Project)" `
            --build-arg "DLL_NAME=$($image.Dll)" `
            --tag "seniorcare/$($image.Name):1.0.0" `
            .
        if ($LASTEXITCODE -ne 0) {
            throw "Falló la construcción de la imagen seniorcare/$($image.Name):1.0.0."
        }
    }

    docker build `
        --file src/Web/SeniorCare.Portal.Web/Dockerfile `
        --tag seniorcare/portal-web:1.0.0 `
        .
    if ($LASTEXITCODE -ne 0) {
        throw 'Falló la construcción de la imagen seniorcare/portal-web:1.0.0.'
    }
}
finally {
    Pop-Location
}
