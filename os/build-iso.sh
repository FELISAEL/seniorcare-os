#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "La construcción de la ISO debe ejecutarse con sudo."
  exit 1
fi

if ! command -v lb >/dev/null 2>&1; then
  echo "Falta live-build. Instalalo con: sudo apt install live-build"
  exit 1
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="${SCRIPT_DIR}/.build"
OUTPUT_DIR="${SCRIPT_DIR}/../artifacts"

# URL del portal SeniorCare que quedará embebida en esta ISO.
# Se puede sobrescribir exportando SENIORCARE_URL antes del build, por ejemplo
# para apuntar a un portal accesible desde la máquina de destino:
#   sudo SENIORCARE_URL=http://portal.local:8088 bash build-iso.sh
# Si la variable no se define, se usa el mismo valor por defecto que el kiosco.
DEFAULT_SENIORCARE_URL="http://localhost:8088"

if [[ -n "${SENIORCARE_URL+definida}" ]]; then
  SENIORCARE_URL_ORIGEN="variable de entorno"
else
  SENIORCARE_URL="${DEFAULT_SENIORCARE_URL}"
  SENIORCARE_URL_ORIGEN="valor predeterminado"
fi

validar_seniorcare_url() {
  local url="$1"
  local resto autoridad host puerto
  # Solo caracteres que pueden escribirse sin comillas en kiosk.env, que luego
  # se incorpora con "source" desde POSIX sh (sin espacios, $, comillas, ` o \).
  local re_seguro='^[]A-Za-z0-9._~:/?#@%=+[-]+$'

  if [[ -z "${url}" ]]; then
    echo "SENIORCARE_URL está vacía; definí una URL http:// o https://." >&2
    return 1
  fi

  if [[ "${url}" == *[[:space:]]* ]]; then
    echo "SENIORCARE_URL no puede contener espacios ni saltos de línea." >&2
    return 1
  fi

  if [[ "${url}" == *[[:cntrl:]]* ]]; then
    echo "SENIORCARE_URL no puede contener caracteres de control." >&2
    return 1
  fi

  if [[ "${url}" != "http://"* && "${url}" != "https://"* ]]; then
    echo "SENIORCARE_URL debe ser una URL absoluta http:// o https://." >&2
    return 1
  fi

  resto="${url#*://}"

  # No se permiten query strings ('?') ni fragmentos ('#'): podrían arrastrar
  # datos sensibles (tokens, identificadores) al kiosk.env y a la pantalla de
  # arranque. El sistema solo necesita esquema, host, puerto opcional y ruta
  # opcional.
  if [[ "${resto}" == *[?#]* ]]; then
    echo "SENIORCARE_URL no debe incluir query string ('?') ni fragmento ('#')." >&2
    return 1
  fi

  autoridad="${resto%%/*}"

  if [[ -z "${autoridad}" ]]; then
    echo "SENIORCARE_URL debe incluir un host después del esquema." >&2
    return 1
  fi

  if [[ "${autoridad}" == *@* ]]; then
    echo "SENIORCARE_URL no debe incluir credenciales embebidas (user:pass@host)." >&2
    return 1
  fi

  if [[ "${autoridad}" == *:* ]]; then
    host="${autoridad%%:*}"
    puerto="${autoridad#*:}"
  else
    host="${autoridad}"
    puerto=""
  fi

  # El host tiene que ser un nombre o IP real: rechaza casos como "http://",
  # "http://:8088", "http://?dato" o "http://#fragmento".
  if ! [[ "${host}" =~ ^[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?$ ]]; then
    echo "SENIORCARE_URL debe incluir un host real (nombre o IP) después del esquema." >&2
    return 1
  fi

  if [[ -n "${puerto}" ]] && ! [[ "${puerto}" =~ ^[0-9]+$ ]]; then
    echo "SENIORCARE_URL tiene un puerto inválido: '${puerto}'." >&2
    return 1
  fi

  if ! [[ "${url}" =~ $re_seguro ]]; then
    echo "SENIORCARE_URL tiene caracteres que no pueden escribirse de forma segura en kiosk.env." >&2
    return 1
  fi

  return 0
}

if ! validar_seniorcare_url "${SENIORCARE_URL}"; then
  echo "Se cancela la construcción sin modificar ${BUILD_DIR} ni ejecutar live-build." >&2
  exit 1
fi

echo "SeniorCare OS: la ISO apuntará al portal ${SENIORCARE_URL} (${SENIORCARE_URL_ORIGEN})."

rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}" "${OUTPUT_DIR}"
cp -a "${SCRIPT_DIR}/live-build/config" "${BUILD_DIR}/config"

# La URL validada se escribe solo en la copia de construcción, que vive dentro
# del directorio ignorado os/.build. El kiosk.env versionado no se toca.
BUILD_KIOSK_ENV="${BUILD_DIR}/config/includes.chroot/etc/seniorcare/kiosk.env"
mkdir -p "$(dirname -- "${BUILD_KIOSK_ENV}")"
printf 'SENIORCARE_URL=%s\n' "${SENIORCARE_URL}" > "${BUILD_KIOSK_ENV}"

cd "${BUILD_DIR}"

lb config \
  --architectures amd64 \
  --archive-areas "main contrib non-free-firmware" \
  --binary-images iso-hybrid \
  --bootappend-live "boot=live components quiet splash username=seniorcare hostname=seniorcare" \
  --debian-installer live \
  --distribution trixie \
  --iso-application "SeniorCare OS" \
  --iso-publisher "Proyecto académico SeniorCare" \
  --iso-volume "SENIORCARE_OS"

lb build

ISO_PATH="$(find . -maxdepth 1 -type f -name '*.iso' -print -quit)"
if [[ -z "${ISO_PATH}" ]]; then
  echo "La construcción terminó sin producir una ISO."
  exit 1
fi

cp "${ISO_PATH}" "${OUTPUT_DIR}/seniorcare-os-amd64.iso"
echo "ISO creada en ${OUTPUT_DIR}/seniorcare-os-amd64.iso"

