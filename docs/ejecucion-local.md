# Ejecución local

## Windows

1. Instalar Docker Desktop y habilitar WSL 2.
2. Abrir PowerShell en `seniorcare-os`.
3. Crear configuración local:

   ```powershell
   Copy-Item .env.example .env
   ```

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

Perfiles opcionales de Docker Compose:

```powershell
docker compose --profile analytics up -d etl-worker
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

## Restablecer datos de desarrollo

```powershell
docker compose down --volumes
docker compose up --build
```
