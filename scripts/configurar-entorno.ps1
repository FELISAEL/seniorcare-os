# Prepara el archivo .env con secretos propios de esta instalación.
#
# - Si no existe .env, lo crea desde .env.example y genera valores aleatorios para cada
#   secreto (base de datos, JWT y contraseñas iniciales). No los muestra en pantalla.
# - Si ya existe, nunca sobrescribe un secreto válido. Si encuentra valores de ejemplo:
#     * sin base de datos creada todavía, los reemplaza por valores nuevos;
#     * con la base ya creada, se detiene y explica qué variables corregir, porque cambiar
#       POSTGRES_PASSWORD sobre una base existente dejaría a la API sin acceso.
# Las contraseñas iniciales solo se usan para crear usuarios que no existen: cambiarlas no
# modifica las cuentas ya creadas.
param(
    [string]$Directorio = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$Proyecto
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun.ps1')

if (-not $Proyecto) { $Proyecto = Get-SeniorCareProject }

$envPath = Join-Path $Directorio '.env'
$examplePath = Join-Path $Directorio '.env.example'
$volume = "${Proyecto}_seniorcare-postgres"

function Test-ExistingDatabase {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { return $false }
    return Test-DockerVolume $volume
}

if (-not (Test-Path $envPath)) {
    if (Test-ExistingDatabase) {
        throw "Existe la base de datos '$volume' pero falta el archivo .env. Restaurá el .env original (o su respaldo); generar uno nuevo dejaría a la API sin acceso a la base."
    }
    if (-not (Test-Path $examplePath)) {
        throw "No se encontró .env.example en $Directorio."
    }

    $lines = Read-EnvLines $examplePath
    $lines = $lines | ForEach-Object {
        $line = $_
        foreach ($key in $script:SecretKeys) {
            if ($line -match "^\s*$key\s*=") { $line = "$key=$(New-SeniorCareSecret)" }
        }
        $line
    }
    Write-EnvLines $envPath $lines
    Write-Host 'Se creó .env con secretos nuevos para esta instalación (no se muestran).' -ForegroundColor Cyan
    Write-Host 'Las contraseñas iniciales de los usuarios están en las variables SEED_*_PASSWORD de .env.'
    return
}

$lines = Read-EnvLines $envPath
$insecure = Get-InsecureSecretKeys $lines
if ($insecure.Count -eq 0) {
    Write-Host 'Configuración .env verificada: los secretos son propios de esta instalación.' -ForegroundColor Green
    return
}

if (Test-ExistingDatabase) {
    throw ("El archivo .env tiene valores de ejemplo o ya publicados en: " + ($insecure -join ', ') +
        ". La base '$volume' ya existe, así que no se cambian automáticamente. " +
        "Reemplazá esas variables a mano (para POSTGRES_PASSWORD, cambiá también la contraseña dentro de PostgreSQL).")
}

$lines = $lines | ForEach-Object {
    $line = $_
    foreach ($key in $insecure) {
        if ($line -match "^\s*$key\s*=") { $line = "$key=$(New-SeniorCareSecret)" }
    }
    $line
}
foreach ($key in $insecure) {
    if (-not ($lines | Where-Object { $_ -match "^\s*$key\s*=" })) { $lines += "$key=$(New-SeniorCareSecret)" }
}
Write-EnvLines $envPath $lines
Write-Host ("Se generaron valores nuevos para: " + ($insecure -join ', ') + ' (no se muestran).') -ForegroundColor Cyan
