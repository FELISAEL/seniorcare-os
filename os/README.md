# SeniorCare OS basado en Debian

Esta carpeta convierte una instalación de Debian en un sistema especializado
para personas adultas mayores. El sistema inicia sesión automáticamente con un
usuario restringido y abre el portal SeniorCare en pantalla completa. El login central reconoce el rol y lleva a la persona a su panel; en un equipo dedicado al adulto mayor se utiliza su cuenta `resident`.

## Probar sobre Debian

En una máquina virtual Debian con conexión a internet:

```bash
sudo bash install-kiosk.sh http://DIRECCION_DEL_SERVIDOR:8088
sudo reboot
```

Para probar cuando Docker se ejecuta en la misma computadora:

```bash
sudo bash install-kiosk.sh http://localhost:8088
```

## Construir una ISO

La construcción debe ejecutarse dentro de Debian:

```bash
sudo apt update
sudo apt install live-build
sudo bash build-iso.sh
```

El resultado se guardará en `artifacts/seniorcare-os-amd64.iso`.

## Cambiar la dirección del servidor

Editar:

```text
/etc/seniorcare/kiosk.env
```

Después reiniciar la sesión gráfica:

```bash
sudo systemctl restart lightdm
```

## Controles aplicados

- Usuario `seniorcare` sin privilegios administrativos.
- Inicio automático mediante LightDM.
- Openbox como sesión gráfica mínima.
- Chromium en modo kiosco.
- Teclas y menús del navegador ocultos.
- Protector de pantalla y suspensión desactivados durante la sesión.
- Reapertura del navegador si se cierra.
- Comprobación periódica de conectividad mediante un temporizador de systemd.

