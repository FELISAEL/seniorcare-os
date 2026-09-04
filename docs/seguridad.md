# Seguridad y límites

## Controles incorporados

- Contraseñas derivadas con PBKDF2 y sal aleatoria.
- Tokens JWT firmados con validación de emisor, audiencia, firma y expiración.
- Secreto JWT con longitud mínima validada al iniciar.
- Políticas centralizadas por rol.
- Autorización por recurso mediante `resident_id`.
- Rate limiting en el endpoint de login.
- Middleware global para excepciones y correlation IDs.
- Encabezados HTTP de seguridad.
- Bases lógicas separadas por dominio.
- Configuración sensible mediante variables de entorno/Secrets.
- Usuario Linux restringido para modo kiosco.
- Health checks y observabilidad.

## Principio importante

La interfaz no es la barrera de seguridad. Aunque un usuario escriba una URL o
modifique el JavaScript, la API vuelve a comprobar el JWT, el rol y, cuando
corresponde, el residente vinculado.

## Antes de producción

- Reemplazar todas las credenciales de demostración.
- Usar HTTPS de extremo a extremo.
- Guardar secretos en un gestor de secretos.
- Rotar el secreto JWT y usar una estrategia de revocación/refresh tokens si el
  producto pasa de MVP académico a producción real.
- Limitar la exposición de PostgreSQL y del puerto interno de `seniorcare-api`
  (`8080`); publicar externamente solo los puntos necesarios a través de
  Nginx/Ingress (portal en `8088` y, si aplica, la API detrás del proxy).
- Configurar copias de seguridad y restauraciones probadas.
- Añadir auditoría persistente de acciones sensibles.
- Definir protocolo humano para SOS y tiempos de respuesta.
- Realizar pruebas de accesibilidad con personas adultas mayores.
- Ejecutar pruebas de penetración y revisión de dependencias.

SeniorCare no sustituye servicios médicos o de emergencia oficiales.
