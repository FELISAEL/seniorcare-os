# SeniorCare OS

SeniorCare OS es una plataforma distribuida y un entorno Linux en modo kiosco
orientado a personas adultas mayores. Incluye un portal público único,
autenticación centralizada por roles, paneles especializados, recordatorios de
medicamentos, asistencia y SOS, videollamadas, aplicación móvil para el equipo
de cuidado, una API unificada .NET con módulos, PostgreSQL, ETL y observabilidad.

## Arquitectura

La organización sigue el mismo principio de separación de responsabilidades
que se tomó como referencia de AYUMED, adaptado al stack de SeniorCare:

```text
Portal público / Login   (http://localhost:8088)
        |
        v
Nginx del portal  --/api/*-->  seniorcare-api:8080  (local: http://localhost:7000)
        |
        +-- admin ------> /panel/administracion/
        +-- caregiver --> /panel/cuidador/
        +-- resident ---> /panel/adulto-mayor/
        +-- family -----> /panel/familiar/

API unificada (un proceso, cinco módulos: Identity, Care, Emergency,
Communication, Analytics)
Controller -> Service -> Repository -> Modelo/PostgreSQL
                   |
                   +-> Policies / JWT / autorización por recurso
```

No se copió el código PHP de AYUMED. Se trasladó su disciplina arquitectónica
a .NET: controladores pequeños, servicios para reglas de negocio, repositorios
para persistencia, middleware transversal y autorización centralizada. El
backend es una sola API ASP.NET Core (`api/SeniorCare.Api.csproj`); cada módulo
conserva su separación interna dentro de `api/app/`.

## Tecnologías

- C# y .NET 10.
- ASP.NET Core: una API unificada con cinco módulos internos.
- JWT y políticas de autorización por rol y por residente vinculado.
- Portal web/PWA accesible servido por Nginx.
- .NET MAUI para la aplicación del equipo de cuidado.
- PostgreSQL.
- Docker Compose y Kubernetes.
- OpenTelemetry y Aspire Dashboard.
- Debian Linux + Chromium en modo kiosco.

## Inicio rápido

### Requisitos

- Docker Desktop con Docker Compose.
- Al menos 6 GB de memoria disponible para Docker.
- Puertos `5438`, `7000`, `8088` disponibles (y `18888`, `4317`, `4318` si se
  usa el perfil `monitoring`).

### Iniciar

Hacer doble clic en:

```text
INICIAR_SENIORCARE.cmd
```

O ejecutar:

```powershell
Copy-Item .env.example .env
docker compose up --build
```

Portal público y login único:

- <http://localhost:8088> (portal + paneles por rol)
- <http://localhost:7000/health> (salud de la API unificada)
- Monitoreo (perfil `monitoring`): <http://localhost:18888>

Componentes opcionales por perfil de Docker Compose:

```powershell
docker compose --profile analytics up -d etl-worker
docker compose --profile monitoring up -d aspire-dashboard
```

Usuarios de demostración:

| Perfil | Usuario | Contraseña | Panel |
| --- | --- | --- | --- |
| Administrador | `admin` | `Cambiar123!` | `/panel/administracion/` |
| Cuidador | `cuidador` | `Cuidador123!` | `/panel/cuidador/` |
| Adulto mayor | `maria` | `Maria123!` | `/panel/adulto-mayor/` |
| Familiar | `ana` | `Familia123!` | `/panel/familiar/` |

Las credenciales anteriores son únicamente de desarrollo.

### Prueba de integración

Con el sistema iniciado:

```powershell
.\scripts\prueba-integracion.ps1
```

### Detener

```powershell
docker compose down
```

Para borrar también los datos locales:

```powershell
docker compose down --volumes
```

## Componentes

La API unificada `seniorcare-api` (`api/SeniorCare.Api.csproj`) expone cinco
módulos internos:

| Módulo de la API | Responsabilidad | Prefijo |
| --- | --- | --- |
| Identity | Login, usuarios, roles, JWT y panel asignado | `/api/identity` |
| Care | Residentes, medicamentos, horarios y tomas | `/api/care` |
| Emergency | SOS, asistencia y seguimiento | `/api/emergencies` |
| Communication | Videollamadas | `/api/communication` |
| Analytics | Métricas operativas | `/api/analytics` |

Resto de componentes:

| Componente | Responsabilidad |
| --- | --- |
| ETL Worker | Consolidación de métricas (opcional, perfil `analytics`) |
| Portal Web | Sitio público + login + paneles por rol; Nginx reenvía `/api/*` a `seniorcare-api:8080` |
| Mobile | Operación para administrador/cuidador |
| SeniorCare OS | Linux restringido con modo kiosco |

Docker Compose declara solo `postgres`, `seniorcare-api` y `portal-web` como
servicios activos; `etl-worker` y `aspire-dashboard` quedan tras perfiles. Las
cinco APIs separadas y los puertos `7001–7005` ya no forman parte de la
arquitectura.

## Documentación

- [Arquitectura](docs/arquitectura.md)
- [Autenticación y roles](docs/autenticacion-roles.md)
- [QA de arquitectura](docs/qa-arquitectura.md)
- [Seguridad](docs/seguridad.md)
- [Ejecución local](docs/ejecucion-local.md)
- [Manual de usuario](docs/manual-usuario.md)
- [Manual técnico](docs/manual-tecnico.md)
- [Alcance del MVP](docs/alcance-mvp.md)
- [Plan Scrum](docs/scrum.md)
- [Matriz de requisitos](docs/matriz-requisitos.md)
- [Diagramas UML](docs/uml/README.md)
- [Botón físico SOS por USB](docs/boton-sos-usb.md)
