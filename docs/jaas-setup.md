# Videollamadas con Jitsi alojado por 8x8 (JaaS)

Con JaaS la llamada se muestra dentro del panel del adulto mayor y nadie queda
esperando a que "llegue un moderador": el servidor firma un token (JWT) que
entra a todos como moderador. No necesitas montar ningún servidor.

## 1. Crear la cuenta y la llave

1. Entra a <https://jaas.8x8.vc> y crea una cuenta (puede ser la secundaria).
2. En la consola, abre **API Keys** y pulsa **Add API key** (generar llave).
3. Descarga el archivo de llave privada (termina en `.pk`). Guárdalo fuera del
   repositorio. **No se vuelve a mostrar.**
4. Copia el **Key ID** completo. Tiene la forma
   `vpaas-magic-cookie-xxxxxxxx/yyyyyy`.

## 2. Convertir la llave a una sola línea (base64)

PowerShell:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\ruta\a\tu-llave.pk"))
```

Linux / macOS:

```bash
base64 -w0 tu-llave.pk
```

## 3. Completar el `.env`

```env
JAAS_KEY_ID=vpaas-magic-cookie-xxxxxxxx/yyyyyy
JAAS_PRIVATE_KEY=<el texto largo de base64>
```

`JAAS_APP_ID` es opcional: se deduce del Key ID (la parte antes de `/`).
Estos valores solo van en tu `.env` local; no los subas a git ni los pegues en chats.

## 4. Aplicar

```bash
docker compose up -d --build seniorcare-api portal-web
```

Si `JAAS_KEY_ID` queda vacío, el sistema usa el Jitsi público como respaldo.

## Cómo funciona

- El residente pulsa videollamada: la API crea la sala `8x8.vc/<AppID>/<sala>`
  y devuelve una dirección con su token. Se incrusta en el panel.
- Se envía el correo al familiar con una dirección con token válido unas horas.
- Cuidadores y administradores se unen desde sus listas; cada consulta genera
  un token nuevo.
- El residente espera dentro de la llamada hasta que el familiar entra.
