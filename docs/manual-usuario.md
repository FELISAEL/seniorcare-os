# Manual de usuario

## Iniciar SeniorCare

1. Ejecutar `INICIAR_SENIORCARE.cmd`.
2. Esperar el mensaje de disponibilidad.
3. Abrir `http://localhost:8088`.
4. Iniciar sesión. SeniorCare abre automáticamente el panel según el rol.

## Adulto mayor

El panel está diseñado con controles grandes y pocas decisiones visibles.
Permite consultar medicamentos, confirmar tomas, pedir asistencia, generar SOS
y solicitar videollamada.

## Familiar

El familiar accede únicamente a la persona adulta mayor vinculada a su cuenta.
Puede consultar información autorizada, historial de alertas y comunicación,
pero no obtiene funciones operativas reservadas al cuidador.

## Cuidador

Puede consultar residentes, planes de medicamentos, alertas activas,
videollamadas y métricas operativas. Puede atender y resolver alertas según las
políticas del backend.

## Administrador

Incluye las funciones del equipo de cuidado y además administración de cuentas.
Al crear una cuenta `resident` o `family`, debe vincularla con un residente.

## Aplicación móvil

La app MAUI está orientada al equipo operativo (`admin` y `caregiver`). Permite
consultar alertas, atender/resolver y unirse a videollamadas. El familiar usa su
panel web específico.

## Cerrar sesión

Usar **Salir/Cerrar sesión** dentro del panel. El token local se elimina y el
usuario regresa al portal público.
