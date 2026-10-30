# Funciones compartidas por iniciar.ps1, detener.ps1 y configurar-entorno.ps1.
# Se cargan con: . (Join-Path $PSScriptRoot 'comun.ps1')

# Proyecto Docker de este repositorio. El proyecto anterior (seniorcare-os) usa los mismos
# puertos y otro volumen: estos scripts nunca lo inician, detienen ni modifican.
$script:SeniorCareProject = 'seniorcare-nuevo'
$script:LegacyProject = 'seniorcare-os'

# Variables que guardan secretos y deben tener valores propios de cada instalación.
$script:SecretKeys = @(
    'POSTGRES_PASSWORD',
    'JWT_SECRET',
    'SEED_ADMIN_PASSWORD',
    'SEED_CAREGIVER_PASSWORD',
    'SEED_RESIDENT_PASSWORD',
    'SEED_FAMILY_PASSWORD'
)

# SHA-256 de valores ya publicados (ejemplos y configuración de desarrollo antigua).
# Es la misma lista que usa la API (InsecureSecretGuard); se guardan solo las huellas.
$script:KnownLeakedHashes = @(
    '859814b8f4a0c6b70f70843eeb694683e6539808b4cb90283c7bfc7c808e006c',
    '066e6948d62f9204b3d06e05c0820b4afd17fc5c77f47bd24f837e3deebe3ebe',
    '9036999e208fdfb79f9e193fa209c06490fa8f1661c3ad824d6e7dc662607b7f',
    '2baa2556378327d3b21b959da0ff5c74a453b765476baf3e3965d1e36750f870',
    'd13832997abd60c1021ec4437e1144ca41293974141a8ebc15ebb45a3e289e71',
    '26f8c13cdf7a3ffb6e0ba7701e529372bae51718c0fdca0926284b62f5d75071',
    'c07579c89b14be276ec97b95c717f04732a13354f4abb2e646c1e866540b1189',
    '0127947321d95f3cd165613633559173a850adafb114acf5ddee6c7a4902ca7b'
)

function Get-SeniorCareProject {
    # Permite otro nombre (p. ej. un entorno de pruebas), pero nunca el del proyecto anterior.
    $name = if ($env:SENIORCARE_COMPOSE_PROJECT) { $env:SENIORCARE_COMPOSE_PROJECT.Trim() } else { $script:SeniorCareProject }
    if ($name -ieq $script:LegacyProject) {
        throw "El proyecto '$($script:LegacyProject)' es el sistema anterior y estos scripts no lo administran. Usá '$($script:SeniorCareProject)'."
    }
    if ($name -notmatch '^[a-z0-9][a-z0-9_-]*$') {
        throw "Nombre de proyecto Docker no válido: '$name'."
    }
    return $name
}

function Test-InsecureSecret([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $true }
    $trimmed = $Value.Trim()
    foreach ($marker in @('CAMBIAR', 'CHANGE_ME', 'CHANGEME', 'REEMPLAZAR')) {
        if ($trimmed.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { return $true }
    }
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($trimmed))
    }
    finally { $sha.Dispose() }
    $hash = -join ($bytes | ForEach-Object { $_.ToString('x2') })
    return $script:KnownLeakedHashes -contains $hash
}

function New-SeniorCareSecret {
    # 48 bytes aleatorios en base64url: 64 caracteres sin '=', '+', '/', ';' ni comillas,
    # seguros dentro de las cadenas de conexión y del archivo .env.
    $bytes = New-Object byte[] 48
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Read-EnvLines([string]$Path) {
    return [System.IO.File]::ReadAllLines($Path, [System.Text.Encoding]::UTF8)
}

function Write-EnvLines([string]$Path, [string[]]$Lines) {
    # UTF-8 sin BOM: Docker Compose lee la primera variable mal si el archivo trae BOM.
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, (($Lines -join "`n") + "`n"), $encoding)
}

function Get-EnvValue([string[]]$Lines, [string]$Key) {
    $line = $Lines | Where-Object { $_ -match "^\s*$Key\s*=" } | Select-Object -Last 1
    if (-not $line) { return $null }
    return ($line -split '=', 2)[1].Trim()
}

# Nombres (nunca valores) de los secretos inseguros o ausentes en un .env.
function Get-InsecureSecretKeys([string[]]$Lines) {
    return @($script:SecretKeys | Where-Object { Test-InsecureSecret (Get-EnvValue $Lines $_) })
}

function Test-DockerVolume([string]$Name) {
    $names = docker volume ls --quiet --filter "name=^$Name`$"
    return [bool]($names | Where-Object { $_ -eq $Name })
}
