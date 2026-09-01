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
  chromium \
  curl \
  lightdm \
  openbox \
  unclutter \
  x11-xserver-utils

cp -a "${OVERLAY_DIR}/." /

if ! id seniorcare >/dev/null 2>&1; then
  useradd \
    --create-home \
    --shell /bin/bash \
    --comment "SeniorCare Resident" \
    seniorcare
fi

install -d -o seniorcare -g seniorcare /home/seniorcare/.config/openbox
cp /etc/skel/.config/openbox/autostart \
  /home/seniorcare/.config/openbox/autostart
chown seniorcare:seniorcare /home/seniorcare/.config/openbox/autostart
chmod 750 /home/seniorcare/.config/openbox/autostart

sed -i \
  "s|^SENIORCARE_URL=.*$|SENIORCARE_URL=${KIOSK_URL}|" \
  /etc/seniorcare/kiosk.env

systemctl enable lightdm.service
systemctl enable seniorcare-health.timer

echo
echo "SeniorCare OS quedó configurado."
echo "Interfaz del residente: ${KIOSK_URL}"
echo "Reiniciá con: sudo reboot"

