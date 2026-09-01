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
| 5438 | PostgreSQL de desarrollo |
| 7001 | Identity API |
| 7002 | Care API |
| 7003 | Emergency API |
| 7004 | Communication API |
| 7005 | Analytics API |
| 8088 | Portal público + todos los paneles |
| 18888 | Aspire Dashboard |
| 4317 | OpenTelemetry OTLP/gRPC |
| 4318 | OpenTelemetry OTLP/HTTP |

## Organización backend

```text
src/BuildingBlocks/SeniorCare.Shared/
  Auth/
  Middleware/
  Data/
  Web/

src/Services/SeniorCare.*.Api/
  Controllers/
  Services/
  Repositories/
  Domain/
  Program.cs
```

Identity agrega `Security/` para hash y emisión de JWT.

## Persistencia

PostgreSQL ejecuta `infra/postgres/init/001-databases.sql` al crear el volumen.
Cada API inicializa sus tablas de forma idempotente.

## Autenticación

Roles: `admin`, `caregiver`, `resident`, `family`.

Identity API deriva contraseñas con PBKDF2-SHA256, emite JWT y devuelve también
`panelPath`. Las cuentas `resident` y `family` se vinculan mediante
`resident_id`. Los servicios aplican políticas desde `SeniorCare.Shared`.

## ETL

1. Extrae horarios/tomas desde `care_db` y alertas desde `emergency_db`.
2. Agrupa por fecha y calcula adherencia.
3. Realiza upsert en `analytics_db.daily_metrics`.

## Observabilidad

Los servicios pueden enviar trazas, métricas y logs por OTLP a Aspire
Dashboard. El dashboard sin autenticación es solamente para desarrollo.

## Backup local

```powershell
docker compose exec -T postgres pg_dumpall -U seniorcare > seniorcare-respaldo.sql
```

## Aplicación móvil

Android usa `http://10.0.2.2:8088` desde el emulador. En dispositivo físico debe
configurarse la IP alcanzable del equipo que ejecuta Docker.

## ISO

Consultar `os/README.md`.
