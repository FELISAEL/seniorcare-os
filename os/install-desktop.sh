#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Este instalador debe ejecutarse con sudo."
  exit 1
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
KIOSK_URL="${1:-http://localhost:8088}"
OVERLAY_DIR="${SCRIPT_DIR}/live-build/config/includes.chroot"

if [[ ! -d "${OVERLAY_DIR}" ]]; then
  echo "No se encontró la configuración de SeniorCare OS."
  exit 1
fi

apt-get update
DEBIAN_FRONTEND=noninteractive apt-get install -y \
  xserver-xorg \
  lightdm \
  lightdm-gtk-greeter \
  xfce4 \
  xfce4-terminal \
  xfce4-taskmanager \
  xfce4-notifyd \
  thunar \
  thunar-archive-plugin \
  mousepad \
  ristretto \
  firefox-esr \
  chromium \
  python3 \
  python3-psutil \
  python3-tk \
  htop \
  dbus-x11 \
  curl \
  ca-certificates \
  fonts-noto-core \
  network-manager \
  network-manager-gnome \
  zenity \
  x11-xserver-utils \
  libglib2.0-bin

# Opcional (solo útil dentro de VirtualBox): portapapeles compartido.
DEBIAN_FRONTEND=noninteractive apt-get install -y virtualbox-guest-x11 || true

cp -a "${OVERLAY_DIR}/." /

chmod 755 /usr/local/bin/seniorcare-app /usr/local/bin/seniorcare-monitor \
  /usr/local/bin/seniorcare-set-url /usr/local/lib/seniorcare/monitor.py \
  /usr/local/lib/seniorcare/trust-launchers /usr/local/lib/seniorcare/set-wallpaper
chmod 750 /usr/local/lib/seniorcare/health-check

if ! id seniorcare >/dev/null 2>&1; then
  useradd --create-home --shell /bin/bash --comment "SeniorCare" seniorcare
  echo "Definí la contraseña del usuario seniorcare:"
  passwd seniorcare
fi

install -d -o seniorcare -g seniorcare /home/seniorcare/Desktop
cp /etc/skel/Desktop/*.desktop /home/seniorcare/Desktop/
chown -R seniorcare:seniorcare /home/seniorcare/Desktop
install -d -o seniorcare -g seniorcare /home/seniorcare/.config/gtk-3.0
cp -n /etc/skel/.config/gtk-3.0/gtk.css /home/seniorcare/.config/gtk-3.0/gtk.css
chown seniorcare:seniorcare /home/seniorcare/.config/gtk-3.0/gtk.css
chmod 755 /home/seniorcare/Desktop/*.desktop

sed -i \
  "s|^SENIORCARE_URL=.*$|SENIORCARE_URL=${KIOSK_URL}|" \
  /etc/seniorcare/kiosk.env

systemctl enable lightdm.service
systemctl enable seniorcare-health.timer

echo
echo "SeniorCare OS (escritorio XFCE) quedó configurado."
echo "Portal SeniorCare: ${KIOSK_URL}"
echo "Reiniciá con: sudo reboot"
