$ErrorActionPreference = 'Stop'

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $projectRoot

function Login-SeniorCare([string]$Username, [string]$Password) {
    Invoke-RestMethod -Method Post -Uri 'http://localhost:8088/api/identity/login' `
        -ContentType 'application/json' `
        -Body (@{ username = $Username; password = $Password } | ConvertTo-Json)
}

function Assert-Panel($Login, [string]$Expected) {
    if ($Login.panelPath -ne $Expected) {
        throw "Panel incorrecto para $($Login.user.username). Esperado: $Expected; recibido: $($Login.panelPath)"
    }
}

function Bearer($Login) { @{ Authorization = "Bearer $($Login.accessToken)" } }

function Assert-Forbidden([scriptblock]$Action, [string]$Name) {
    try {
        & $Action | Out-Null
        throw "La prueba '$Name' debía devolver 403 y permitió el acceso."
    }
    catch {
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 403) {
            Write-Host "[OK] Bloqueo: $Name" -ForegroundColor Green
            return
        }
        if ($_.Exception.Message -like "La prueba '*") { throw }
        throw "La prueba '$Name' falló con un resultado distinto de 403: $($_.Exception.Message)"
    }
}

try {
    $health = Invoke-RestMethod -Uri 'http://localhost:7000/health' -TimeoutSec 8
    if ($health.status -ne 'healthy') {
        throw 'La API unificada no está saludable.'
    }
    Write-Host "[OK] API unificada ($($health.service))" -ForegroundColor Green

    $admin = Login-SeniorCare 'admin' 'Cambiar123!'
    $caregiver = Login-SeniorCare 'cuidador' 'Cuidador123!'
    $residentLogin = Login-SeniorCare 'maria' 'Maria123!'
    $family = Login-SeniorCare 'ana' 'Familia123!'

    Assert-Panel $admin '/panel/administracion/'
    Assert-Panel $caregiver '/panel/cuidador/'
    Assert-Panel $residentLogin '/panel/adulto-mayor/'
    Assert-Panel $family '/panel/familiar/'
    Write-Host '[OK] Login y panel por rol' -ForegroundColor Green

    $residents = Invoke-RestMethod -Uri 'http://localhost:8088/api/care/residents' -Headers (Bearer $admin)
    if ($residents.Count -lt 1) { throw 'No se encontró el residente de demostración.' }
    $resident = $residents[0]

    $medications = Invoke-RestMethod `
        -Uri "http://localhost:8088/api/care/residents/$($resident.id)/medications/today" `
        -Headers (Bearer $admin)
    if ($medications.Count -lt 1) { throw 'No se encontraron medicamentos de demostración.' }

    $ownResident = Invoke-RestMethod -Uri 'http://localhost:8088/api/care/me/residents' -Headers (Bearer $residentLogin)
    if ($ownResident.Count -ne 1) { throw 'La cuenta del adulto mayor no quedó limitada a su residente vinculado.' }

    $familyResident = Invoke-RestMethod -Uri 'http://localhost:8088/api/care/me/residents' -Headers (Bearer $family)
    if ($familyResident.Count -ne 1) { throw 'La cuenta familiar no quedó limitada a su residente vinculado.' }

    Assert-Forbidden { Invoke-RestMethod -Uri 'http://localhost:8088/api/care/residents' -Headers (Bearer $residentLogin) } 'adulto mayor no lista todos los residentes'
    Assert-Forbidden { Invoke-RestMethod -Uri 'http://localhost:8088/api/care/residents' -Headers (Bearer $family) } 'familiar no lista todos los residentes'
    Assert-Forbidden { Invoke-RestMethod -Uri 'http://localhost:8088/api/identity/users' -Headers (Bearer $caregiver) } 'cuidador no administra usuarios'
    Assert-Forbidden { Invoke-RestMethod -Uri 'http://localhost:8088/api/analytics/recent?days=7' -Headers (Bearer $family) } 'familiar no accede a analítica global'

    $metrics = Invoke-RestMethod -Uri 'http://localhost:8088/api/analytics/recent?days=7' -Headers (Bearer $admin)

    Write-Host "[OK] Residentes: $($residents.Count)" -ForegroundColor Green
    Write-Host "[OK] Medicamentos: $($medications.Count)" -ForegroundColor Green
    Write-Host "[OK] Registros ETL: $($metrics.Count)" -ForegroundColor Green
    Write-Host ''
    Write-Host 'La prueba de integración y autorización terminó correctamente.' -ForegroundColor Cyan
}
finally {
    Pop-Location
}
