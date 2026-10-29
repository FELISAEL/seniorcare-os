# SeniorCare OS: Debian 13 personalizado

Esta carpeta convierte Debian 13 (trixie) en **SeniorCare OS**, una distribución
propia con escritorio multitarea, en la misma línea en que los fabricantes
personalizan Android o Linux: se parte de Debian, se agregan paquetes, se
configura el escritorio y se preinstalan aplicaciones propias.

## Qué incluye

- **Escritorio XFCE** con barra de tareas, menú de aplicaciones y varias
  ventanas abiertas a la vez.
- **SeniorCare** preinstalada: acceso en el escritorio y en el menú, y se abre
  automáticamente al iniciar sesión (como aplicación en su propia ventana).
- **Carpetas** (Thunar), **navegador web** (Firefox), **terminal**, editor de
  texto y visor de imágenes.
- **Monitor de rendimiento** propio (`seniorcare-monitor`), estilo Administrador
  de tareas: CPU total y por núcleo, memoria y swap, lectura/escritura de
  disco, y RAM, CPU y disco por aplicación, con botón «Finalizar tarea».
  También se incluyen `xfce4-taskmanager` y `htop`.
- **Configurar servidor** (icono del escritorio): cambia la dirección del portal sin reconstruir la ISO.
- **Fondo de escritorio** de SeniorCare: degradado verde azulado oscuro con los
  colores de la marca, ajustado para cubrir toda la pantalla (se aplica la primera
  vez que se inicia sesión; luego se puede cambiar). Archivo:
  `/usr/share/backgrounds/seniorcare/seniorcare-wallpaper.png`.
- Teclado latinoamericano y zona horaria de Guatemala de fábrica.
- Guest Additions de VirtualBox (si el paquete está disponible): en VirtualBox activa
  **Dispositivos → Portapapeles compartido → Bidireccional** para copiar y pegar.
- Comprobación periódica de conectividad con el portal (systemd timer).

## Probar sobre un Debian 13 ya instalado

```bash
sudo bash install-desktop.sh http://DIRECCION_DEL_SERVIDOR:8088
sudo reboot
```

Con Docker en la misma computadora: `sudo bash install-desktop.sh http://localhost:8088`

Para abrir solo el monitor: `seniorcare-monitor`

## Construir la ISO

Dentro de Debian 13 (o WSL2 Debian 13):

```bash
sudo apt update && sudo apt install live-build
sudo bash build-iso.sh
```

Resultado: `artifacts/seniorcare-os-amd64.iso`. Usuario de la ISO de
demostración: `seniorcare` / `seniorcare` (cambiarla fuera de pruebas).
Recomendado en VirtualBox: 2 vCPU, 4096 MB de RAM, 25 GB, UEFI.

## Videollamada (cámara y micrófono) en VirtualBox

La videollamada se muestra dentro de SeniorCare y usa JaaS (Jitsi de 8x8), así
que la VM necesita internet. Para que la VM vea la cámara y el micrófono de la
laptop:

1. Con la VM encendida: **Dispositivos → Cámaras web →** elige la cámara.
2. **Dispositivos → Audio → Habilitar entrada de audio.**
3. La primera vez, Chromium pide permiso de cámara y micrófono: pulsa **Permitir**.

SeniorCare abre el portal con la opción de Chromium que trata esa dirección
(`http://10.0.2.2:8088`) como segura; sin ella el navegador bloquea la cámara.

## Búsqueda automática del servidor

Al abrir SeniorCare, el sistema prueba en orden: la dirección guardada, `10.0.2.2`
(la laptop vista desde VirtualBox en modo NAT), el nombre del equipo anfitrión
(`NOMBRE.local` y `NOMBRE`) y la puerta de enlace de la red. Usa la primera que
responda y la recuerda. Si ninguna responde, pregunta si se quiere escribir la
dirección a mano (icono **Configurar servidor**).

`build-iso.sh` detecta el nombre de la laptop con `powershell.exe` cuando se
ejecuta dentro de WSL; también se puede indicar con `SENIORCARE_HOSTNAME=MI-PC`.

Para que el nombre del equipo se resuelva en una VM en modo NAT, ejecutar una
vez en PowerShell de Windows (con la VM apagada):

```text
& "C:\Program Files\Oracle\VirtualBox\VBoxManage.exe" modifyvm "SeniorCare OS" --natdnshostresolver1 on
```

## Cambiar la dirección del servidor

Editar `/etc/seniorcare/kiosk.env` (variable `SENIORCARE_URL`) y volver a abrir
SeniorCare.

## Estructura

- `live-build/config/package-lists/`: paquetes del sistema.
- `live-build/config/includes.chroot/`: archivos que se copian al sistema
  (lanzadores, monitor, configuración de LightDM, escritorio del usuario).
- `live-build/config/hooks/live/`: configuración final dentro de la ISO.
- `usr/local/lib/seniorcare/monitor.py`: código del monitor de rendimiento.

## Limitaciones conocidas

- El portal necesita un servidor SeniorCare accesible por red (Docker).
- El inicio automático de sesión puede variar entre versiones de VirtualBox.
- Con 2 GB de RAM funciona, pero con pocas ventanas abiertas; se recomiendan 4 GB.
