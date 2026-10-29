# Videollamadas con Google Meet

SeniorCare crea una reunión de Meet por llamada usando la API de Google Meet.
Se hace **una sola vez** con una cuenta de Google (puede ser la misma de Gmail).

## 1. Proyecto en Google Cloud
1. Entra a https://console.cloud.google.com y crea un proyecto (ej. `seniorcare`).
2. **APIs y servicios → Biblioteca** → busca **Google Meet REST API** → *Habilitar*.
3. **APIs y servicios → Pantalla de consentimiento de OAuth**: tipo *Externo*, nombre `SeniorCare`, tu correo.
   - En *Agregar o quitar permisos* añade `https://www.googleapis.com/auth/meetings.space.created` y `https://www.googleapis.com/auth/meetings.space.readonly` (el segundo permite saber si el familiar ya se conectó).
   - En *Usuarios de prueba* agrega tu cuenta de Google.
   - **Publica la app** (*Publicar aplicación* / "En producción"). Si se queda en "Pruebas", el token caduca a los 7 días.
     Google mostrará "app no verificada" al autorizar: es normal para uso propio (Avanzado → Continuar).

## 2. Credenciales
1. **Credenciales → Crear credenciales → ID de cliente de OAuth** → tipo *Aplicación web*.
2. En *URI de redireccionamiento autorizados* agrega: `https://developers.google.com/oauthplayground`
3. Copia el **ID de cliente** y el **Secreto de cliente**.

## 3. Obtener el refresh token (una vez)
1. Abre https://developers.google.com/oauthplayground
2. Engranaje ⚙ (arriba a la derecha) → marca **Use your own OAuth credentials** → pega ID y secreto.
3. En el cuadro "Input your own scopes" escribe `https://www.googleapis.com/auth/meetings.space.created https://www.googleapis.com/auth/meetings.space.readonly` → *Authorize APIs* → acepta con tu cuenta.
4. *Exchange authorization code for tokens* → copia el **Refresh token**.

## 4. Configurar `.env` (no se sube a git)
```
GOOGLE_MEET_ENABLED=true
GOOGLE_CLIENT_ID=...apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=...
GOOGLE_REFRESH_TOKEN=...
```
Luego `docker compose up -d --build`. Al iniciar una videollamada, el enlace será `https://meet.google.com/xxx-xxxx-xxx`.
Si algo falla, el sistema usa Jitsi y deja el motivo en el log de la API (`docker compose logs api`).

## Flujo "el residente espera al familiar"
- Requiere: correo del familiar registrado en el residente, `SMTP_ENABLED=true` (ver `.env.example`) y Google Meet configurado.
- El residente pulsa Videollamada → se envía el correo al familiar → el residente ve "Esperando a tu familiar…" hasta que alguien (familiar, cuidador o administrador) entra a la reunión → entonces se abre la llamada.
- Si falta alguno de esos requisitos, la llamada se abre de inmediato como antes.
- Al cambiar los permisos hay que generar un **refresh token nuevo** en el Playground con los dos scopes.
