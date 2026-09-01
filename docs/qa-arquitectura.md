# QA de arquitectura - SeniorCare OS

## Objetivo de la revisión

Tomar AYUMED como referencia de orden arquitectónico y aplicar el mismo nivel de
separación a SeniorCare sin cambiar innecesariamente su stack .NET.

## Hallazgos principales del sistema original

1. Existían roles, JWT y microservicios, pero los frontends de residente y
   administración tenían entradas separadas.
2. La redirección y comprobación de rol estaba demasiado distribuida en cada
   frontend.
3. Varias APIs mezclaban endpoint y acceso a datos en una misma capa.
4. La protección por rol no cubría por sí sola el caso de un residente/familiar
   cambiando manualmente el ID de otro residente en una URL.
5. La aplicación móvil apuntaba al antiguo puerto del panel administrativo.

## Cambios aplicados

- Portal público general único en `8088`.
- Login único con ruta de panel calculada en backend.
- Roles centralizados en `SeniorCareRoles`.
- Políticas centralizadas en `SeniorCarePolicies`.
- `resident_id` dentro del JWT para cuentas vinculadas.
- Autorización por recurso en Care, Emergency y Communication.
- Capas Controller -> Service -> Repository -> Domain.
- Middleware compartido para excepciones, correlation ID y headers.
- Rate limiting de login.
- Paneles independientes para administrador, cuidador, adulto mayor y familiar.
- Administración de cuentas restringida a administrador.
- Aplicación móvil ajustada al portal unificado y equipo operativo.
- Docker, Nginx, Kubernetes, scripts y documentación alineados con el portal
  único.

## Criterios de aceptación

- Un `resident` no puede entrar al panel de `admin` aunque escriba la URL.
- Un `family` no puede administrar usuarios ni resolver alertas.
- Un `resident`/`family` no puede consultar otro residente cambiando el GUID.
- `caregiver` no puede administrar usuarios.
- `admin` puede administrar usuarios y operar el sistema.
- El login devuelve una única ruta de panel de acuerdo con el rol.
- Los paneles vuelven a validar la sesión antes de cargar datos.

## Validación recomendada en el equipo de desarrollo

```powershell
docker compose up --build
.\scripts\prueba-integracion.ps1
```

Además, ejecutar `dotnet build` o compilar mediante Docker en cada cambio de
backend. Antes de producción deben añadirse pruebas automatizadas de integración
y seguridad en CI.

## Validaciones ejecutadas en esta revisión

- JavaScript de portal/paneles: `node --check` sin errores.
- JSON de configuración: parseo correcto.
- XAML y archivos `.csproj`: XML válido.
- `docker-compose.yml` y manifiestos Kubernetes: YAML válido.
- Referencias locales CSS/JS de los HTML: sin archivos faltantes.
- Configuración Nginx: prueba de sintaxis correcta usando upstreams locales de prueba.
- Búsqueda de referencias obsoletas a los antiguos `resident-web`, `admin-web` y puerto `8089`: sin resultados.

### Limitación del entorno de revisión

El contenedor utilizado para esta revisión no incluye `dotnet`, `docker` ni
PowerShell. Por esa razón no se ejecutó aquí `dotnet build`, `docker compose up`
ni `prueba-integracion.ps1`. El proyecto deja esa prueba preparada para
realizarse en Windows/Docker Desktop, donde debe formar parte del criterio final
de aceptación antes de producción.
