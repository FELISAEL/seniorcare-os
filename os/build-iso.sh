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

rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}" "${OUTPUT_DIR}"
cp -a "${SCRIPT_DIR}/live-build/config" "${BUILD_DIR}/config"

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

