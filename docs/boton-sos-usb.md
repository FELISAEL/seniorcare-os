# Botón físico SOS por USB

La PWA escucha la tecla `F9` cuando la pantalla del residente está activa. Un
botón USB configurado como dispositivo HID puede enviar esa tecla sin instalar
un controlador especial.

## Prototipo

1. Utilizar un botón USB programable o una placa compatible con HID.
2. Configurar una pulsación para enviar `F9`.
3. Conectar el dispositivo a SeniorCare OS.
4. Presionar el botón.
5. La interfaz abre la confirmación de emergencia.
6. Confirmar en pantalla para generar la alerta SOS.

La confirmación reduce activaciones accidentales. Para un entorno real se debe
definir si el botón físico enviará la alerta directamente o mantendrá la
confirmación, según el análisis de riesgos y las necesidades de los residentes.
