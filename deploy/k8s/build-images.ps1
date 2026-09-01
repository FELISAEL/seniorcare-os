$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Push-Location $root

try {
    $services = @(
        @{ Name = 'identity-api'; Project = 'src/Services/SeniorCare.Identity.Api/SeniorCare.Identity.Api.csproj'; Dll = 'SeniorCare.Identity.Api.dll' },
        @{ Name = 'care-api'; Project = 'src/Services/SeniorCare.Care.Api/SeniorCare.Care.Api.csproj'; Dll = 'SeniorCare.Care.Api.dll' },
        @{ Name = 'emergency-api'; Project = 'src/Services/SeniorCare.Emergency.Api/SeniorCare.Emergency.Api.csproj'; Dll = 'SeniorCare.Emergency.Api.dll' },
        @{ Name = 'communication-api'; Project = 'src/Services/SeniorCare.Communication.Api/SeniorCare.Communication.Api.csproj'; Dll = 'SeniorCare.Communication.Api.dll' },
        @{ Name = 'analytics-api'; Project = 'src/Services/SeniorCare.Analytics.Api/SeniorCare.Analytics.Api.csproj'; Dll = 'SeniorCare.Analytics.Api.dll' },
        @{ Name = 'etl-worker'; Project = 'src/Workers/SeniorCare.Etl.Worker/SeniorCare.Etl.Worker.csproj'; Dll = 'SeniorCare.Etl.Worker.dll' }
    )

    foreach ($service in $services) {
        docker build `
            --file infra/docker/DotNetService.Dockerfile `
            --build-arg "PROJECT_PATH=$($service.Project)" `
            --build-arg "DLL_NAME=$($service.Dll)" `
            --tag "seniorcare/$($service.Name):1.0.0" `
            .
    }

    docker build `
        --file src/Web/SeniorCare.Portal.Web/Dockerfile `
        --tag seniorcare/portal-web:1.0.0 `
        .
}
finally {
    Pop-Location
}
