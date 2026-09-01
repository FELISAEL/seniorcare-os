# Arquitectura frontend de SeniorCare OS

## Regla principal

- `src/Web/SeniorCare.Portal.Web/views/`: **solo HTML**.
- `src/Web/SeniorCare.Portal.Web/public/css/`: estilos separados por responsabilidad.
- `src/Web/SeniorCare.Portal.Web/public/js/`: comportamiento separado por responsabilidad.
- `src/Web/SeniorCare.Portal.Web/public/pwa/`: manifest y service worker.

Los paneles no contienen CSS ni JavaScript junto a sus vistas. El `Dockerfile` fusiona ambas carpetas únicamente en la imagen de Nginx para servirlas, sin mezclar el código fuente.

## CSS

Cada módulo dispone de un `app.css` que funciona como punto de entrada mediante `@import`. Administración, cuidador y familia reutilizan la base `panels/care-team` y conservan únicamente sus reglas específicas. El panel de adulto mayor separa layout, acciones, medicamentos, accesibilidad y responsive.

## JavaScript

- `js/auth`: sesión y autenticación.
- `js/shared`: utilidades transversales, incluido el manejo central de errores.
- `js/public`: portal/login.
- `js/panels/care-team`: residentes, medicamentos, alertas, analítica, comunicación y usuarios.
- `js/panels/adulto-mayor`: eventos, medicamentos, alertas, videollamada, offline y accesibilidad.
- `js/panels/familiar`: seguimiento de residente, medicamentos, alertas y comunicación.

`app.js` es el orquestador de cada área y no concentra toda la lógica.

## Manejo de errores

El backend ya utiliza `ExceptionHandlingMiddleware` y `CorrelationIdMiddleware`; no se modificaron. El frontend incorpora `js/shared/error-handler.js`, que centraliza errores no controlados, promesas rechazadas, mensajes visibles y registro en consola. Las cargas paralelas usan `SeniorCareErrors.allSettled` para reportar fallos parciales sin derribar el panel completo.
