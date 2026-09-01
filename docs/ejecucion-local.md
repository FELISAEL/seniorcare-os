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

El portal es único. Después del login, Identity API devuelve el panel
correspondiente al rol.

## Verificación de APIs

```powershell
Invoke-RestMethod http://localhost:7001/health
Invoke-RestMethod http://localhost:7002/health
Invoke-RestMethod http://localhost:7003/health
Invoke-RestMethod http://localhost:7004/health
Invoke-RestMethod http://localhost:7005/health
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
