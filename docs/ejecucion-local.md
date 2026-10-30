# Ejecución local

## Windows

1. Instalar Docker Desktop y habilitar WSL 2.
2. Abrir PowerShell en `seniorcare-os`.
3. Crear configuración local con secretos propios (no muestra los valores y nunca
   sobrescribe un `.env` existente):

   ```powershell
   powershell -File .scriptsconfigurar-entorno.ps1
   ```

   No copiar `.env.example` tal cual: la API se niega a iniciar con sus valores
   `CAMBIAR_*` o con secretos ya publicados en el repositorio.

4. Construir e iniciar:

   ```powershell
   docker compose up --build
   ```

5. Abrir `http://localhost:8088`.

El portal es único. Después del login, el módulo Identity de la API unificada
devuelve el panel correspondiente al rol.

## Arranque asistido y detención

```powershell
.\INICIAR_SENIORCARE.cmd   # o: powershell -File .\scripts\iniciar.ps1
.\DETENER_SENIORCARE.cmd   # o: powershell -File .\scripts\detener.ps1
```

El ETL de analítica (`etl-worker`) se inicia junto con el resto del sistema.

Los scripts usan siempre el proyecto Docker `seniorcare-nuevo` (también es el
`name:` de `docker-compose.yml`). Nunca inician, detienen ni modifican el sistema
anterior `seniorcare-os`; si está en ejecución, `iniciar.ps1` se detiene y avisa.
Para comprobar sin iniciar nada: `powershell -File .scriptsiniciar.ps1 -SoloVerificar`.
Pruebas de estos scripts: `powershell -File .scriptspruebasprobar-arranque-seguro.ps1`.

Perfil opcional de Docker Compose:

```powershell
docker compose --profile monitoring up -d aspire-dashboard
```

## Verificación de la API

Existe una sola API (`seniorcare-api`) publicada en `http://localhost:7000`. El
portal (`http://localhost:8088`) enruta `/api/*` hacia ella a través de Nginx
(`seniorcare-api:8080`).

```powershell
Invoke-RestMethod http://localhost:7000/health
```

## Prueba integral de roles

```powershell
.\scripts\prueba-integracion.ps1
```

La prueba valida salud, login de los cuatro perfiles, rutas de panel y controles
de autorización positivos/negativos.

## Emergencias sin conexión

El panel del Adulto Mayor guarda cada SOS o pedido de asistencia en una cola del
navegador con un identificador y la hora real en que se pulsó, y lo reintenta solo
hasta que el servidor confirma que lo registró. El servidor no duplica reintentos
con el mismo identificador y guarda la hora real (`created_at`) y la de llegada
(`received_at`); el cuidador ve "Llegó con retraso" cuando difieren más de 2 minutos.
Mientras haya un aviso sin confirmar, la pantalla indica que el cuidador todavía
no lo recibió. La cola vive en ese navegador: si se borran sus datos o se pierde el
equipo, un aviso pendiente se pierde.

## Confirmación de dosis

Se aceptan las dosis de hoy y de `DOSE_CONFIRMATION_MAX_DAYS_LATE` días anteriores
(por defecto 1, máximo 7), nunca fechas futuras ni días anteriores a la creación del
medicamento. El valor 1 cubre una confirmación hecha sin conexión antes de medianoche
que llega después. Si el personal de cuidado necesita registrar días más viejos, se
conversa y se ajusta esta variable en `.env`.

## Restablecer datos de desarrollo

> Borra la base de datos de `seniorcare-nuevo`. No usar sobre datos reales.

```powershell
docker compose down --volumes
docker compose up --build
```
