# Pruebas del arranque seguro (Etapa 11.1). No inicia, detiene ni modifica contenedores:
# trabaja en carpetas temporales y solo consulta Docker en modo lectura.
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\pruebas\probar-arranque-seguro.ps1
$ErrorActionPreference = 'Stop'
$scripts = Resolve-Path (Join-Path $PSScriptRoot '..')
$root = Resolve-Path (Join-Path $scripts '..')
. (Join-Path $scripts 'comun.ps1')

$results = New-Object System.Collections.Generic.List[object]
function Check([string]$Name, [scriptblock]$Test) {
    try {
        & $Test
        $results.Add([pscustomobject]@{ Prueba = $Name; Resultado = 'OK' })
        Write-Host "[OK]    $Name" -ForegroundColor Green
    }
    catch {
        $results.Add([pscustomobject]@{ Prueba = $Name; Resultado = 'FALLA' })
        Write-Host "[FALLA] $Name :: $($_.Exception.Message)" -ForegroundColor Red
    }
}
function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-Throws([scriptblock]$Action, [string]$Expected) {
    try { & $Action } catch {
        if ($Expected -and $_.Exception.Message -notlike "*$Expected*") { throw "Error inesperado: $($_.Exception.Message)" }
        return $_.Exception.Message
    }
    throw 'Se esperaba un error y no ocurrió.'
}
function New-TempDir {
    $path = Join-Path ([System.IO.Path]::GetTempPath()) ("seniorcare-prueba-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $path | Out-Null
    Copy-Item (Join-Path $root '.env.example') $path
    return $path
}
function Get-FileSha([string]$Path) { (Get-FileHash -Algorithm SHA256 $Path).Hash }
$sinVolumen = 'seniorcare-prueba-sin-volumen'
$conVolumen = 'seniorcare-auditoria'   # entorno aislado de auditoría: su volumen existe

Check 'docker compose sin -p usa el proyecto seniorcare-nuevo' {
    Push-Location $root
    try { $config = docker compose config --format json | ConvertFrom-Json } finally { Pop-Location }
    Assert ($config.name -eq 'seniorcare-nuevo') "Proyecto por defecto: $($config.name)"
}

Check 'Proyecto por defecto de los scripts' {
    $previous = $env:SENIORCARE_COMPOSE_PROJECT
    try {
        $env:SENIORCARE_COMPOSE_PROJECT = $null
        Assert ((Get-SeniorCareProject) -eq 'seniorcare-nuevo') 'No es seniorcare-nuevo'
    } finally { $env:SENIORCARE_COMPOSE_PROJECT = $previous }
}

Check 'Los scripts se niegan a administrar el proyecto anterior seniorcare-os' {
    $previous = $env:SENIORCARE_COMPOSE_PROJECT
    try {
        $env:SENIORCARE_COMPOSE_PROJECT = 'seniorcare-os'
        Assert-Throws { Get-SeniorCareProject } 'sistema anterior' | Out-Null
        $env:SENIORCARE_COMPOSE_PROJECT = 'SeniorCare-OS'
        Assert-Throws { Get-SeniorCareProject } 'sistema anterior' | Out-Null
        $env:SENIORCARE_COMPOSE_PROJECT = 'x; docker rm'
        Assert-Throws { Get-SeniorCareProject } 'no válido' | Out-Null
    } finally { $env:SENIORCARE_COMPOSE_PROJECT = $previous }
}

Check 'iniciar.ps1 y detener.ps1 siempre pasan -p y nunca usan -v' {
    foreach ($file in 'iniciar.ps1', 'detener.ps1') {
        $lines = Get-Content (Join-Path $scripts $file)
        # Toda línea de código que ejecuta docker compose debe fijar el proyecto.
        $calls = @($lines | ForEach-Object { $_.Trim() } | Where-Object { $_ -like 'docker compose*' })
        Assert ($calls.Count -gt 0) "$file no ejecuta docker compose"
        foreach ($call in $calls) {
            Assert ($call -like 'docker compose -p $project *') "$file sin -p: $call"
        }
        Assert (-not ($lines -match 'down\s+-v')) "$file usa down -v"
    }
}

Check 'iniciar.ps1 -SoloVerificar resuelve seniorcare-nuevo con el .env actual' {
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scripts 'iniciar.ps1') -SoloVerificar 2>&1 | Out-String
    Assert ($LASTEXITCODE -eq 0 -or $null -eq $LASTEXITCODE) "Código de salida $LASTEXITCODE`n$output"
    Assert ($output -match "proyecto 'seniorcare-nuevo'") "Salida: $output"
}

Check 'iniciar.ps1 -SoloVerificar rechaza SENIORCARE_COMPOSE_PROJECT=seniorcare-os' {
    $previous = $env:SENIORCARE_COMPOSE_PROJECT
    try {
        $env:SENIORCARE_COMPOSE_PROJECT = 'seniorcare-os'
        # El proceso hijo escribe el error en stderr: aquí se espera, no debe cortar la prueba.
        $ErrorActionPreference = 'Continue'
        $output = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scripts 'iniciar.ps1') -SoloVerificar 2>&1 | Out-String
        Assert ($LASTEXITCODE -ne 0) 'Debió fallar'
        Assert ($output -match 'sistema anterior') "Salida: $output"
    } finally { $env:SENIORCARE_COMPOSE_PROJECT = $previous }
}

Check 'Instalación nueva: genera 6 secretos aleatorios distintos sin mostrarlos' {
    $dir = New-TempDir
    try {
        $output = & (Join-Path $scripts 'configurar-entorno.ps1') -Directorio $dir -Proyecto $sinVolumen 6>&1 | Out-String
        $lines = Read-EnvLines (Join-Path $dir '.env')
        $values = $script:SecretKeys | ForEach-Object { Get-EnvValue $lines $_ }
        Assert ($values.Count -eq 6) 'Faltan secretos'
        Assert (($values | Select-Object -Unique).Count -eq 6) 'Secretos repetidos'
        foreach ($value in $values) {
            Assert ($value.Length -eq 64) "Longitud $($value.Length)"
            Assert ($value -match '^[A-Za-z0-9_-]+$') 'Caracteres no seguros para cadenas de conexión'
            Assert (-not (Test-InsecureSecret $value)) 'Secreto inseguro'
            Assert ($output -notlike "*$value*") 'Un secreto apareció en la salida'
        }
        Assert ((Get-InsecureSecretKeys $lines).Count -eq 0) 'Quedaron marcadores'
        Assert ((Get-EnvValue $lines 'SEED_ADMIN_USERNAME') -eq 'admin') 'Se alteró una variable no secreta'
        $bytes = [System.IO.File]::ReadAllBytes((Join-Path $dir '.env'))
        Assert (-not ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB)) '.env con BOM'
    } finally { Remove-Item -Recurse -Force $dir }
}

Check 'Segunda ejecución: no sobrescribe secretos existentes' {
    $dir = New-TempDir
    try {
        & (Join-Path $scripts 'configurar-entorno.ps1') -Directorio $dir -Proyecto $sinVolumen 6>&1 | Out-Null
        $before = Get-FileSha (Join-Path $dir '.env')
        & (Join-Path $scripts 'configurar-entorno.ps1') -Directorio $dir -Proyecto $sinVolumen 6>&1 | Out-Null
        Assert ($before -eq (Get-FileSha (Join-Path $dir '.env'))) 'El .env cambió'
    } finally { Remove-Item -Recurse -Force $dir }
}

Check 'Sin base creada: reemplaza solo los valores de ejemplo' {
    $dir = New-TempDir
    try {
        & (Join-Path $scripts 'configurar-entorno.ps1') -Directorio $dir -Proyecto $sinVolumen 6>&1 | Out-Null
        $path = Join-Path $dir '.env'
        $lines = Read-EnvLines $path
        $jwt = Get-EnvValue $lines 'JWT_SECRET'
        Write-EnvLines $path ($lines | ForEach-Object { if ($_ -match '^SEED_FAMILY_PASSWORD=') { 'SEED_FAMILY_PASSWORD=CAMBIAR_CONTRASENA' } else { $_ } })
        & (Join-Path $scripts 'configurar-entorno.ps1') -Directorio $dir -Proyecto $sinVolumen 6>&1 | Out-Null
        $after = Read-EnvLines $path
        Assert (-not (Test-InsecureSecret (Get-EnvValue $after 'SEED_FAMILY_PASSWORD'))) 'No se reemplazó'
        Assert ((Get-EnvValue $after 'JWT_SECRET') -eq $jwt) 'Cambió un secreto válido'
    } finally { Remove-Item -Recurse -Force $dir }
}

Check 'Con base existente: no toca .env inseguro y nombra la variable sin mostrar valores' {
    $dir = New-TempDir
    try {
        & (Join-Path $scripts 'configurar-entorno.ps1') -Directorio $dir -Proyecto $sinVolumen 6>&1 | Out-Null
        $path = Join-Path $dir '.env'
        Write-EnvLines $path ((Read-EnvLines $path) | ForEach-Object { if ($_ -match '^POSTGRES_PASSWORD=') { 'POSTGRES_PASSWORD=CAMBIAR_CONTRASENA' } else { $_ } })
        $before = Get-FileSha $path
        $message = Assert-Throws { & (Join-Path $scripts 'configurar-entorno.ps1') -Directorio $dir -Proyecto $conVolumen } 'POSTGRES_PASSWORD'
        Assert ($message -notlike '*CAMBIAR_CONTRASENA*') 'El mensaje mostró el valor'
        Assert ($before -eq (Get-FileSha $path)) 'El .env cambió'
    } finally { Remove-Item -Recurse -Force $dir }
}

Check 'Con base existente y sin .env: se detiene en lugar de generar claves nuevas' {
    $dir = New-TempDir
    try {
        Assert-Throws { & (Join-Path $scripts 'configurar-entorno.ps1') -Directorio $dir -Proyecto $conVolumen } 'falta el archivo .env' | Out-Null
        Assert (-not (Test-Path (Join-Path $dir '.env'))) 'Se creó .env'
    } finally { Remove-Item -Recurse -Force $dir }
}

Check '.env.example: todos sus secretos se reconocen como inseguros' {
    $lines = Read-EnvLines (Join-Path $root '.env.example')
    Assert ((Get-InsecureSecretKeys $lines).Count -eq 6) 'Algún ejemplo pasaría como válido'
}

Check '.env actual: sin secretos de ejemplo ni publicados (solo nombres)' {
    $insecure = Get-InsecureSecretKeys (Read-EnvLines (Join-Path $root '.env'))
    Assert ($insecure.Count -eq 0) ("Inseguros: " + ($insecure -join ', '))
}

Check 'Las huellas bloqueadas coinciden con las de la API' {
    $cs = Get-Content (Join-Path $root 'api\app\Core\Auth\InsecureSecretGuard.cs') -Raw
    $api = [regex]::Matches($cs, '"([0-9a-f]{64})"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object
    $ps = $script:KnownLeakedHashes | Sort-Object
    Assert ((Compare-Object $api $ps | Measure-Object).Count -eq 0) 'Listas distintas'
}

$failed = @($results | Where-Object Resultado -eq 'FALLA').Count
Write-Host ''
Write-Host "Pruebas de arranque seguro: $($results.Count - $failed) aprobadas, $failed fallidas."
if ($failed -gt 0) { exit 1 }
