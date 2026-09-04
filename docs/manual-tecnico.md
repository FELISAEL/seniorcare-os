# Manual técnico

## Requisitos

- Windows 10/11.
- Docker Desktop + WSL 2.
- Git.
- .NET 10 SDK para compilación local fuera de Docker.
- Visual Studio/VS Code.
- Carga de trabajo .NET MAUI para móvil.
- Debian + `live-build` para construir la ISO.

## Puertos

| Puerto | Componente |
| --- | --- |
| 5438 | PostgreSQL de desarrollo (contenedor en `5432`) |
| 7000 | API unificada `seniorcare-api` (contenedor en `8080`) |
| 8088 | Portal público + todos los paneles (Nginx → `seniorcare-api:8080`) |
| 18888 | Aspire Dashboard (perfil `monitoring`) |
| 4317 | OpenTelemetry OTLP/gRPC (perfil `monitoring`) |
| 4318 | OpenTelemetry OTLP/HTTP (perfil `monitoring`) |

El worker `etl-worker` no publica puertos y es opcional mediante el perfil
`analytics`. Las cinco APIs separadas y los puertos `7001–7005` ya no existen.

## Organización backend

Una sola solución (`SeniorCare.slnx`) con cuatro proyectos: `api/SeniorCare.Api`,
`tests/SeniorCare.Api.Tests`, `src/Workers/SeniorCare.Etl.Worker` y
`src/Mobile/SeniorCare.Mobile`.

```text
api/
  Program.cs                     # compone los cinco módulos
  config/*Module.cs              # registro y endpoints por módulo
  config/ServiceDefaultsExtensions.cs  # CORS, telemetría, /health
  app/Core/Auth/                 # hash PBKDF2-SHA256, JWT, políticas
  app/Middleware/                # correlation ID, excepciones, headers
  app/Controllers/<Módulo>/
  app/Services/<Módulo>/
  app/Repositories/<Módulo>/
  app/Models/<Módulo>/
```

Módulos: Identity, Care, Emergency, Communication y Analytics.

## Persistencia

PostgreSQL 17 ejecuta `infra/postgres/init/001-databases.sql` al crear el
volumen (`identity_db`, `care_db`, `emergency_db`, `analytics_db`). Cada módulo
inicializa sus tablas de forma idempotente al arrancar la API.

## Autenticación

Roles: `admin`, `caregiver`, `resident`, `family`.

El módulo Identity deriva contraseñas con PBKDF2-SHA256, emite JWT y devuelve
también `panelPath`. Las cuentas `resident` y `family` se vinculan mediante
`resident_id`. Los módulos aplican políticas compartidas desde
`api/app/Core/Auth`.

## ETL

1. Extrae horarios/tomas desde `care_db` y alertas desde `emergency_db`.
2. Agrupa por fecha y calcula adherencia.
3. Realiza upsert en `analytics_db.daily_metrics`.

## Pruebas automatizadas

```powershell
dotnet test .\tests\SeniorCare.Api.Tests\SeniorCare.Api.Tests.csproj
```

72 pruebas (xUnit + `WebApplicationFactory`) cubren unidad de servicios y
extremo a extremo de los endpoints con JWT. La prueba de integración manual
`scripts/prueba-integracion.ps1` valida además el flujo completo por el portal.

## Observabilidad

La API puede enviar trazas, métricas y logs por OTLP a Aspire Dashboard cuando
`OTEL_EXPORTER_OTLP_ENDPOINT` está definido (perfil `monitoring`). El dashboard
sin autenticación es solamente para desarrollo.

## Backup local

```powershell
docker compose exec -T postgres pg_dumpall -U seniorcare > seniorcare-respaldo.sql
```

## Aplicación móvil

Android usa `http://10.0.2.2:8088` desde el emulador. En dispositivo físico debe
configurarse la IP alcanzable del equipo que ejecuta Docker.

## ISO

Consultar `os/README.md`.
