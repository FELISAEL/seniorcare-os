# SeniorCare OS

SeniorCare OS es una plataforma distribuida y un entorno Linux en modo kiosco
orientado a personas adultas mayores. Incluye un portal público único,
autenticación centralizada por roles, paneles especializados, recordatorios de
medicamentos, asistencia y SOS, videollamadas, aplicación móvil para el equipo
de cuidado, microservicios .NET, PostgreSQL, ETL y observabilidad.

## Arquitectura

La organización sigue el mismo principio de separación de responsabilidades
que se tomó como referencia de AYUMED, adaptado al stack de SeniorCare:

```text
Portal público / Login
        |
        v
Identity API
        |
        +-- admin ------> /panel/administracion/
        +-- caregiver --> /panel/cuidador/
        +-- resident ---> /panel/adulto-mayor/
        +-- family -----> /panel/familiar/

API por servicio
Controller -> Service -> Repository -> Domain/PostgreSQL
                   |
                   +-> Policies / JWT / autorización por recurso
```

No se copió el código PHP de AYUMED. Se trasladó su disciplina arquitectónica
a .NET: controladores pequeños, servicios para reglas de negocio, repositorios
para persistencia, middleware transversal y autorización centralizada.

## Tecnologías

- C# y .NET 10.
- ASP.NET Core para microservicios.
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
- Puertos `5438`, `7001` a `7005`, `8088`, `18888`, `4317` y `4318` disponibles.

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

- <http://localhost:8088>
- Monitoreo: <http://localhost:18888>

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

| Componente | Responsabilidad |
| --- | --- |
| Identity API | Login, usuarios, roles, JWT y panel asignado |
| Care API | Residentes, medicamentos, horarios y tomas |
| Emergency API | SOS, asistencia y seguimiento |
| Communication API | Videollamadas |
| Analytics API | Métricas operativas |
| ETL Worker | Consolidación de métricas |
| Portal Web | Sitio público + login + paneles por rol |
| Mobile | Operación para administrador/cuidador |
| SeniorCare OS | Linux restringido con modo kiosco |

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






docker compose build portal-web
docker compose up -d --force-recreate portal-web