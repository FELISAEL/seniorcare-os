# SeniorCare OS

SeniorCare OS es una plataforma distribuida y un entorno Linux en modo kiosco
orientado a personas adultas mayores. Incluye un portal público único,
autenticación centralizada por roles, paneles especializados, recordatorios de
medicamentos, asistencia y SOS, videollamadas, aplicación móvil para el equipo
de cuidado, una API unificada .NET con módulos, PostgreSQL, ETL y observabilidad.

## Arquitectura

La organización sigue el mismo principio de separación de responsabilidades
que se tomó como referencia de AYUMED, adaptado al stack de SeniorCare:

```text
Portal público / Login   (http://localhost:8088)
        |
        v
Nginx del portal  --/api/*-->  seniorcare-api:8080  (local: http://localhost:7000)
        |
        +-- admin ------> /panel/administracion/
        +-- caregiver --> /panel/cuidador/
        +-- resident ---> /panel/adulto-mayor/
        +-- family -----> /panel/familiar/

API unificada (un proceso, cinco módulos: Identity, Care, Emergency,
Communication, Analytics)
Controller -> Service -> Repository -> Modelo/PostgreSQL
                   |
                   +-> Policies / JWT / autorización por recurso
```

No se copió el código PHP de AYUMED. Se trasladó su disciplina arquitectónica
a .NET: controladores pequeños, servicios para reglas de negocio, repositorios
para persistencia, middleware transversal y autorización centralizada. El
backend es una sola API ASP.NET Core (`api/SeniorCare.Api.csproj`); cada módulo
conserva su separación interna dentro de `api/app/`.

## Tecnologías

- C# y .NET 10.
- ASP.NET Core: una API unificada con cinco módulos internos.
- JWT y políticas de autorización por rol y por residente vinculado.
- Portal web/PWA accesible servido por Nginx.
- .NET MAUI para la aplicación del equipo de cuidado.
- PostgreSQL.
- Docker Compose y Kubernetes.
- OpenTelemetry y Aspire Dashboard.
- Debian Linux + Chromium en modo kiosco.

## Inicio rápido

### Requisitos

- Docker Desktop con Docker Compose.
- Al menos 6 GB de memoria disponible para Docker.
- Puertos `5438`, `7000`, `8088` disponibles (y `18888`, `4317`, `4318` si se
  usa el perfil `monitoring`).

### Iniciar

Hacer doble clic en:

```text
INICIAR_SENIORCARE.cmd
```

O ejecutar:

```powershell
Copy-Item .env.example .env
docker compose up --build
```

Portal público y login único:

- <http://localhost:8088> (portal + paneles por rol)
- <http://localhost:7000/health> (salud de la API unificada)
- Monitoreo (perfil `monitoring`): <http://localhost:18888>

Componentes opcionales por perfil de Docker Compose:

```powershell
docker compose --profile analytics up -d etl-worker
docker compose --profile monitoring up -d aspire-dashboard
```

Usuarios de demostración:

| Perfil | Usuario | Contraseña | Panel |
| --- | --- | --- | --- |
| Administrador | `admin` | `Cambiar123!` | `/panel/administracion/` |
| Cuidador | `cuidador` | `Cuidador123!` | `/panel/cuidador/` |
| Adulto mayor | `maria` | `Maria123!` | `/panel/adulto-mayor/` |
| Familiar | `ana` | `Familia123!` | `/panel/familiar/` |

Las credenciales anteriores son únicamente de desarrollo.

### Prueba de integración

Con el sistema iniciado:

```powershell
.\scripts\prueba-integracion.ps1
```

### Detener

```powershell
docker compose down
```

Para borrar también los datos locales:

```powershell
docker compose down --volumes
```

## Componentes

La API unificada `seniorcare-api` (`api/SeniorCare.Api.csproj`) expone cinco
módulos internos:

| Módulo de la API | Responsabilidad | Prefijo |
| --- | --- | --- |
| Identity | Login, usuarios, roles, JWT y panel asignado | `/api/identity` |
| Care | Residentes, medicamentos, horarios y tomas | `/api/care` |
| Emergency | SOS, asistencia y seguimiento | `/api/emergencies` |
| Communication | Videollamadas | `/api/communication` |
| Analytics | Métricas operativas | `/api/analytics` |

Resto de componentes:

| Componente | Responsabilidad |
| --- | --- |
| ETL Worker | Consolidación de métricas (opcional, perfil `analytics`) |
| Portal Web | Sitio público + login + paneles por rol; Nginx reenvía `/api/*` a `seniorcare-api:8080` |
| Mobile | Operación para administrador/cuidador |
| SeniorCare OS | Linux restringido con modo kiosco |

Docker Compose declara solo `postgres`, `seniorcare-api` y `portal-web` como
servicios activos; `etl-worker` y `aspire-dashboard` quedan tras perfiles. Las
cinco APIs separadas y los puertos `7001–7005` ya no forman parte de la
arquitectura.

## Guía reproducible: construir y demostrar SeniorCare OS

Esta guía documenta, de principio a fin, el procedimiento usado para **construir
la ISO de SeniorCare OS y demostrarla en una máquina virtual**, sobre el
siguiente entorno:

- **Windows 11 x64** como equipo anfitrión.
- **WSL2 con Debian 13** para construir la ISO con `live-build`.
- **Docker Desktop** en Windows, ejecutando el portal, la API y PostgreSQL.
- **VirtualBox 7.2.16** para arrancar la ISO.

> Los bloques de comandos indican siempre el intérprete:
> `# --- PowerShell (Windows) ---` se ejecuta en PowerShell de Windows;
> `# --- Bash (Debian WSL2) ---` se ejecuta dentro de la shell de Debian.
> Sustituí los marcadores `<usuario-linux>` y las variables `$Repo*`, `$Vm*`
> por los valores de tu equipo; no están fijados como universales.

### 1. Arquitectura de la demostración

Flujo de una petición durante la demostración:

```text
SeniorCare OS (Chromium en modo kiosco, dentro de la VM)
        |  http://10.0.2.2:8088   (NAT de VirtualBox -> anfitrión)
        v
Nginx  portal-web   (contenedor Docker en el anfitrión, publica 8088 -> 80)
        |  proxy /api/*  ->  http://seniorcare-api:8080
        v
seniorcare-api   (contenedor Docker, publica 7000 -> 8080; .NET 10)
        |  ConnectionStrings__*  ->  Host=postgres;Port=5432
        v
PostgreSQL 17   (contenedor Docker, publica 5438 -> 5432)
```

La **API unificada** es un solo proceso con cinco módulos internos:

| Módulo | Prefijo | Responsabilidad |
| --- | --- | --- |
| Identity | `/api/identity` | Login, usuarios, roles, JWT, panel asignado |
| Care | `/api/care` | Residentes, medicamentos, horarios y tomas |
| Emergency | `/api/emergencies` | SOS, pedir asistencia, seguimiento |
| Communication | `/api/communication` | Videollamadas |
| Analytics | `/api/analytics` | Métricas operativas |

Durante esta demostración, **el portal, la API y PostgreSQL se ejecutan como
contenedores Docker en el anfitrión**. La ISO solo aporta el kiosco (Chromium)
que consume ese portal. Ver [Estado y limitaciones](#16-estado-y-limitaciones).

### 2. Requisitos para reproducirlo

| Requisito | Detalle |
| --- | --- |
| Sistema | Windows 11 x64 actualizado |
| WSL2 | Habilitado (`wsl --version` debe responder) |
| Distribución | Debian 13 en WSL2 |
| Contenedores | Docker Desktop con backend WSL2 y Docker Compose v2 |
| Control de versiones | Git (en Windows y en Debian) |
| Virtualización | VirtualBox 7.2.16 |
| CPU | Virtualización habilitada en BIOS/UEFI (VT-x/AMD-V) |
| RAM | 16 GB recomendados (Docker + WSL2 + una VM de 3072 MB) |
| Disco | ~40 GB libres: la ISO ocupa ~2 GB y el árbol de `live-build` en `os/.build/` varias veces eso durante la construcción |

**No es necesario instalar Claude dentro de Debian.** Claude Code se ejecuta
desde Windows y llama a `wsl.exe` para correr comandos en Debian. Instalar
`claude` dentro de la distribución no aporta nada a este procedimiento.

### 3. Instalación de Debian en WSL2

```powershell
# --- PowerShell (Windows) ---
wsl --install -d Debian
```

Cerrá y abrí una terminal nueva (o reiniciá si WSL2 se instaló por primera vez),
y entrá a la distribución:

```powershell
# --- PowerShell (Windows) ---
wsl -d Debian
```

En el **primer arranque** Debian pide crear un **usuario Linux** y su
contraseña. Escribilos de forma interactiva; **no** los pongas en ningún
archivo, script ni comando. Ese usuario queda como `<usuario-linux>` en el resto
de la guía y su `$HOME` es `/home/<usuario-linux>`.

Validá que `sudo` funciona:

```bash
# --- Bash (Debian WSL2) ---
sudo true && echo "sudo OK"
id
```

Si `sudo` falla porque el usuario no está en el grupo `sudo`, agregalo desde
`root` y reiniciá la distribución:

```bash
# --- Bash (Debian WSL2, como root: 'su -') ---
usermod -aG sudo <usuario-linux>
```

```powershell
# --- PowerShell (Windows) ---
wsl --terminate Debian
wsl -d Debian
```

### 4. Herramientas de construcción en Debian

Instalá **solo** el conjunto necesario, con `--no-install-recommends` para
mantener el entorno mínimo y reproducible (evita arrastrar dependencias
opcionales grandes):

```bash
# --- Bash (Debian WSL2) ---
sudo apt-get update
sudo apt-get install -y --no-install-recommends \
  git \
  rsync \
  live-build \
  debootstrap \
  xorriso \
  squashfs-tools \
  mtools \
  dosfstools \
  grub-pc-bin \
  grub-efi-amd64-bin \
  isolinux \
  syslinux-common \
  debian-archive-keyring \
  ca-certificates
```

- `live-build` + `debootstrap`: arman el sistema base y orquestan la
  construcción.
- `xorriso`, `squashfs-tools`, `mtools`, `dosfstools`: generan la imagen ISO,
  el `squashfs` y la partición EFI.
- `grub-pc-bin` + `grub-efi-amd64-bin` + `isolinux` + `syslinux-common`:
  cargadores para **arranque BIOS y UEFI** en la misma ISO híbrida.
- `debian-archive-keyring` + `ca-certificates`: verificación de firmas y TLS de
  los repositorios.

### 5. Copiar el repositorio al sistema de archivos de Linux

**No construyas la ISO desde `/mnt/c`.** `live-build` ejecuta `debootstrap`,
monta *bind mounts*, entra en `chroot`, crea nodos de dispositivo y fija
propietarios, permisos y bits `setuid`. El disco de Windows montado en
`/mnt/c` pasa por el puente DrvFs/9p, que:

- es lento con miles de archivos pequeños;
- no representa fielmente propietarios, permisos, enlaces simbólicos ni `setuid`;
- bloquea operaciones de `mount`/`chroot` que `live-build` necesita.

Construí siempre sobre el **ext4 nativo** de la distribución (`/home/...`).

Origen (repositorio en Windows):

```text
/mnt/c/test/seniorcare-os
```

Destino recomendado (dentro de Debian):

```text
/home/<usuario-linux>/seniorcare-os
```

Copia segura con `rsync`, **sin sobrescribir archivos que ya existan en el
destino**, conservando `.git/` y excluyendo los directorios de trabajo:

```bash
# --- Bash (Debian WSL2) ---
rsync -a --ignore-existing \
  --exclude 'os/.build/' \
  --exclude '.build/' \
  --exclude 'artifacts/' \
  /mnt/c/test/seniorcare-os/  "$HOME/seniorcare-os/"
```

- `--ignore-existing` no toca archivos ya presentes en el destino.
- `.git/` **no** está excluido: se copia y conserva el historial.
- `os/.build/` y `artifacts/` son artefactos de construcción; no se copian.

**Actualizar una copia Debian que ya existe.** No repitas el `rsync`: podría
sobrescribir `.git/` o cambios locales de esa copia. Traé `main` directamente
desde el repositorio de Windows con un avance rápido (*fast-forward*):

```bash
# --- Bash (Debian WSL2) ---
cd "$HOME/seniorcare-os"
git status --short                       # debe estar vacío
git fetch /mnt/c/test/seniorcare-os main
git merge --ff-only FETCH_HEAD
git status --short
git log -1 --oneline
```

**Detenete** si `git status --short` no está vacío en **cualquiera** de los dos
repositorios (el de Windows o el de Debian): resolvé o guardá esos cambios antes
de sincronizar. `git merge --ff-only` solo adelanta el puntero de la rama; nunca
crea un *merge commit* ni pisa trabajo local, y falla en vez de fusionar si el
avance rápido no es posible.

Normalizá permisos en la copia de Linux:

```bash
# --- Bash (Debian WSL2) ---
cd "$HOME/seniorcare-os"

# Directorios a 755 y archivos normales a 644 (sin tocar .git/)
find . -path ./.git -prune -o -type d -exec chmod 755 {} +
find . -path ./.git -prune -o -type f -exec chmod 644 {} +

# Restaurar el bit de ejecución solo en los archivos que Git marca como 100755
git ls-files -s \
  | awk '$1 == "100755" { sub(/^[0-7]+ [0-9a-f]+ [0-9]+\t/, ""); print }' \
  | tr '\n' '\0' \
  | xargs -0 -r chmod 755
```

Los ejecutables versionados con modo `100755` en Git (por ejemplo
`os/build-iso.sh`, `os/install-kiosk.sh`, los *hooks* de `live-build` y
`usr/local/lib/seniorcare/health-check`) quedan en `755`; el resto en `644`.

### 6. Construcción de la ISO estándar

Desde la copia en `/home/<usuario-linux>/seniorcare-os`:

```bash
# --- Bash (Debian WSL2) ---
cd "$HOME/seniorcare-os"
sudo ./os/build-iso.sh
```

- Debe ejecutarse **con `sudo`** y requiere `live-build` (`lb`) instalado.
- Sin variables, usa la URL por defecto **`http://localhost:8088`**, la misma
  que el kiosco espera cuando Docker corre en la misma máquina que el navegador.
- El espacio de trabajo es `os/.build/` (ignorado por Git; se borra y recrea en
  cada ejecución).
- Salida: **`artifacts/seniorcare-os-amd64.iso`** (relativa a la raíz del
  repositorio).

> **La ISO estándar solo funciona si el portal está disponible dentro del mismo
> sistema donde corre Chromium** (por eso su URL por defecto es
> `http://localhost:8088`). Para la **demostración actual en VirtualBox con
> NAT** se necesita la **variante** construida con
> `SENIORCARE_URL=http://10.0.2.2:8088` (ver §7). La ISO **todavía no incluye
> Docker, la API ni PostgreSQL**: esos servicios siguen ejecutándose en el
> anfitrión.

Comprobación de que la ISO arranca en **BIOS y UEFI** (imagen híbrida, también
apta para USB):

```bash
# --- Bash (Debian WSL2) ---
xorriso -indev artifacts/seniorcare-os-amd64.iso -report_el_torito plain
# Debe listar una imagen de arranque 'BIOS' (/isolinux/isolinux.bin)
# y una imagen 'UEFI' (/boot/grub/efi.img).
```

### 7. Construcción configurable (`SENIORCARE_URL`)

`os/build-iso.sh` acepta la variable de entorno **`SENIORCARE_URL`** para
embeber otra dirección de portal. El script valida el valor y **rechaza**:
URLs sin esquema `http://`/`https://`, sin host real, con credenciales
(`usuario:clave@host`), con *query string* (`?...`), con fragmento (`#...`),
con espacios o con caracteres de control.

Ejemplos genéricos válidos:

```bash
# --- Bash (Debian WSL2) ---
sudo SENIORCARE_URL=http://portal.local:8088   ./os/build-iso.sh
sudo SENIORCARE_URL=https://portal.ejemplo.com ./os/build-iso.sh
```

**Para VirtualBox con red NAT**, el kiosco dentro de la VM debe apuntar al
anfitrión mediante la dirección especial de NAT:

```bash
# --- Bash (Debian WSL2) ---
cd "$HOME/seniorcare-os"
sudo SENIORCARE_URL=http://10.0.2.2:8088 ./os/build-iso.sh
```

- `10.0.2.2` es la dirección fija con la que **VirtualBox NAT** expone el equipo
  anfitrión ante el sistema invitado. **Solo es válida dentro de una VM
  VirtualBox con NAT**; no debe usarse como dirección universal ni en
  instalaciones reales.
- El valor solo se escribe en el `kiosk.env` del espacio de trabajo
  (`os/.build/config/includes.chroot/etc/seniorcare/kiosk.env`); el `kiosk.env`
  versionado del repositorio no se modifica.
- El script **siempre** produce `artifacts/seniorcare-os-amd64.iso`. Para no
  perder la ISO estándar, renombrá la salida de esta construcción antes de
  volver a construir con la URL por defecto:

```bash
# --- Bash (Debian WSL2) ---
mv artifacts/seniorcare-os-amd64.iso artifacts/seniorcare-os-virtualbox-amd64.iso
```

Resultado:

| Archivo | `SENIORCARE_URL` embebida | Uso |
| --- | --- | --- |
| `artifacts/seniorcare-os-amd64.iso` | `http://localhost:8088` | Kiosco y portal en la misma máquina |
| `artifacts/seniorcare-os-virtualbox-amd64.iso` | `http://10.0.2.2:8088` | VM VirtualBox con NAT contra el anfitrión |

### 8. Exportar la ISO a Windows

Calculá el hash del **origen** en Debian:

```bash
# --- Bash (Debian WSL2) ---
sha256sum "$HOME/seniorcare-os/artifacts/seniorcare-os-virtualbox-amd64.iso"
```

Copiá el archivo al repositorio de Windows a través del recurso de WSL
(`\\wsl.localhost\Debian\...`, o `\\wsl$\Debian\...` en compilaciones
anteriores) y calculá el hash del **destino**:

```powershell
# --- PowerShell (Windows) ---
$src = '\\wsl.localhost\Debian\home\<usuario-linux>\seniorcare-os\artifacts\seniorcare-os-virtualbox-amd64.iso'
$dst = 'C:\test\seniorcare-os\artifacts\seniorcare-os-virtualbox-amd64.iso'

Copy-Item $src $dst          # no sobrescribas seniorcare-os-amd64.iso
Get-FileHash $dst -Algorithm SHA256
```

- El SHA-256 del **origen (Debian)** y el del **destino (Windows)** de **la
  misma ISO** deben coincidir; eso confirma que la copia es íntegra.
- **No** trates ningún hash concreto como requisito fijo: cada construcción con
  `live-build` produce un hash distinto (marcas de tiempo, estado del *mirror*).
  Lo que se valida es la igualdad origen = destino, no un valor histórico.
- Nunca sobrescribas `artifacts/seniorcare-os-amd64.iso` con la variante.

### 9. VirtualBox

Instalación con winget:

```powershell
# --- PowerShell (Windows) ---
winget install --exact --id Oracle.VirtualBox --source winget --accept-package-agreements --accept-source-agreements
```

Con Hyper-V/WSL2 activos, VirtualBox 7.x se apoya en la **Plataforma de
hipervisor de Windows** (NEM/WHPX). Habilitala en **PowerShell elevado** y
**reiniciá** después:

```powershell
# --- PowerShell (Windows, ELEVADO) ---
Enable-WindowsOptionalFeature -Online -FeatureName HypervisorPlatform -All -NoRestart
# Reiniciar Windows para que tome efecto.
```

Configuración exacta de la VM de la demostración:

| Parámetro | Valor |
| --- | --- |
| Tipo de SO | Debian (64-bit) |
| Firmware | UEFI (EFI) |
| Secure Boot | Deshabilitado |
| CPU | 2 vCPU |
| RAM | 3072 MB |
| Disco | VDI dinámico de 25 GB |
| Controladora | SATA (AHCI) |
| Gráficos | VMSVGA, 64 MB de VRAM |
| Red | NAT |
| Orden de arranque | DVD primero, luego disco |
| Óptico | ISO variante (`seniorcare-os-virtualbox-amd64.iso`) conectada |

Comandos `VBoxManage` reutilizables (variables de PowerShell, sin rutas
universales):

```powershell
# --- PowerShell (Windows) ---
$VBoxManage = 'C:\Program Files\Oracle\VirtualBox\VBoxManage.exe'
$VmName     = 'SeniorCare OS - Prueba'
$RepoWin    = 'C:\test\seniorcare-os'
$IsoVariant = Join-Path $RepoWin 'artifacts\seniorcare-os-virtualbox-amd64.iso'
$VmBase     = Join-Path $env:USERPROFILE 'VirtualBox VMs'
$Vdi        = Join-Path $VmBase "$VmName\$VmName.vdi"

& $VBoxManage createvm --name $VmName --ostype Debian_64 --basefolder $VmBase --register

& $VBoxManage modifyvm $VmName `
    --firmware efi --cpus 2 --memory 3072 --vram 64 `
    --graphicscontroller vmsvga --nic1 nat `
    --boot1 dvd --boot2 disk --boot3 none --boot4 none
# Secure Boot: deshabilitado por defecto en una VM EFI nueva. Si tu versión
# expone el conmutador:  & $VBoxManage modifyvm $VmName --secure-boot off

& $VBoxManage createmedium disk --filename $Vdi --size 25600 --format VDI --variant Standard
& $VBoxManage storagectl  $VmName --name SATA --add sata --controller IntelAhci --portcount 2 --bootable on
& $VBoxManage storageattach $VmName --storagectl SATA --port 0 --device 0 --type hdd      --medium $Vdi
& $VBoxManage storageattach $VmName --storagectl SATA --port 1 --device 0 --type dvddrive --medium $IsoVariant
```

Para **cambiar solo el medio óptico** más adelante (con la VM apagada):

```powershell
# --- PowerShell (Windows) ---
& $VBoxManage storageattach $VmName --storagectl SATA --port 1 --device 0 --type dvddrive --medium $IsoVariant
```

### 10. Inicio de los servicios en el anfitrión

```powershell
# --- PowerShell (Windows) ---
docker desktop start
docker desktop status                       # esperar hasta 'running'

cd C:\test\seniorcare-os
docker compose up -d postgres seniorcare-api portal-web
docker compose ps
```

Validaciones:

```powershell
# --- PowerShell (Windows) ---
curl.exe -i http://localhost:7000/health    # 200 OK, {"status":"healthy",...}
curl.exe -I http://localhost:8088           # 200 OK (Server: nginx)
```

`postgres` debe figurar como `healthy`; `seniorcare-api` y `portal-web` como
`Up`. Si `docker desktop status` no llega a `running`, esperá y volvé a
consultarlo antes de seguir.

### 11. Arranque de la ISO

```powershell
# --- PowerShell (Windows) ---
& $VBoxManage startvm $VmName --type gui
```

1. En el menú de Debian Live, elegí **`Live system (amd64)`** (es la entrada por
   defecto).
2. **Nunca** selecciones `Install` ni `Start installer` durante la demostración
   en vivo: eso arrancaría el instalador de Debian.
3. Esperá a que se inicie el kiosco (LightDM → Openbox → Chromium a pantalla
   completa).
4. Con la ISO **variante**, Chromium debe abrir `http://10.0.2.2:8088` y mostrar
   el portal público de SeniorCare.
5. El **primer arranque en VirtualBox puede ser lento**: con Hyper-V/WSL2
   presentes, VirtualBox corre sobre NEM/WHPX y el rendimiento es menor que con
   VT-x nativo. Ver [Solución de problemas](#15-solución-de-problemas).

### 12. Demostración funcional

Recorrido sugerido dentro del kiosco:

1. **Página pública** del portal.
2. **Inicio de sesión** con las cuentas de demostración ya documentadas en la
   tabla [Usuarios de demostración](#iniciar) de este README (para el panel del
   adulto mayor, la cuenta `maria`).
3. **Panel del adulto mayor** (`/panel/adulto-mayor/`).
4. **Medicamentos** del día.
5. **Texto grande** (accesibilidad).
6. **Alto contraste**.
7. **Voz** (lectura en voz alta).
8. **Cierre de sesión**.

> **Advertencia:** las acciones **Emergencia**, **Pedir asistencia** y
> **Confirmar que lo tomé** escriben datos reales (alertas, tomas de
> medicación). Usalas **solo en una prueba autorizada**, nunca en una
> demostración de solo lectura.

### 13. Pruebas

```powershell
# --- PowerShell (Windows) ---
dotnet test ".\tests\SeniorCare.Api.Tests\SeniorCare.Api.Tests.csproj" --no-restore
powershell -ExecutionPolicy Bypass -File .\scripts\prueba-integracion.ps1
```

- `dotnet test` ejecuta la batería unitaria/integración de la API.
- `scripts\prueba-integracion.ps1` valida login por rol, autorización por
  recurso y datos sembrados; **requiere la pila levantada** (usa
  `http://localhost:7000` y `http://localhost:8088`).
- **Estado actual: 73 pruebas aprobadas, 0 fallidas, 0 omitidas.**

### 14. Detención segura

```powershell
# --- PowerShell (Windows) ---
docker compose stop        # detiene contenedores, conserva volúmenes y datos
wsl --shutdown             # cierra WSL2 cuando ya no se construyen ISOs
```

Para la VM: **apagado ACPI**, nunca apagado forzado, `reset` ni `savestate`:

```powershell
# --- PowerShell (Windows) ---
& $VBoxManage controlvm $VmName acpipowerbutton
```

Si Debian Live muestra `Please remove the live-medium, then press ENTER`,
pulsá **Enter** dentro de la ventana de la VM para completar el apagado.

> **Prohibido para una detención normal:** `docker compose down --volumes`,
> `docker system prune`, o eliminar volúmenes. Eso borra la base de datos
> sembrada y los datos de la demostración.

### 15. Solución de problemas

| Síntoma | Causa | Qué hacer |
| --- | --- | --- |
| `claude: command not found` dentro de Debian | Claude Code corre en Windows y llama a `wsl.exe`; no se instala en la distribución | Ejecutar Claude Code desde Windows; no instalar `claude` en Debian |
| `docker` responde pero `Cannot connect to the Docker daemon` | El cliente está en PATH pero el *daemon* de Docker Desktop está apagado | `docker desktop start`; esperar `docker desktop status` = `running` |
| Docker Desktop recuerda un servicio antiguo `emergency-api` | Restos de una arquitectura previa (cinco APIs separadas, puertos `7001–7005`); el botón global de *play* de Docker Desktop puede reactivar esa arquitectura | **No** uses el botón global de *play* de Docker Desktop para SeniorCare, porque puede recordar la arquitectura antigua. Abrí PowerShell en la raíz del repositorio y ejecutá `docker compose config --services` (lista los servicios reales del `docker-compose.yml`) y luego `docker compose up -d postgres seniorcare-api portal-web` |
| `http://localhost:8088` no responde **dentro de la VM** | `localhost` en la VM es la propia VM, no Windows | Usar `http://10.0.2.2:8088` (alias de NAT de VirtualBox hacia el anfitrión) |
| Chromium muestra `ERR_CONNECTION_REFUSED` | Docker/los tres servicios no están arriba en el anfitrión, o la ISO tiene embebida `localhost` en vez de `10.0.2.2` | Verificar `curl.exe -I http://localhost:8088` en Windows y que la ISO variante se construyó con `SENIORCARE_URL=http://10.0.2.2:8088` |
| La VM arranca y rinde lento, sobre todo el primer arranque | Con Hyper-V/WSL2, VirtualBox usa NEM/WHPX en vez de VT-x nativo | Esperar; es esperado en este entorno. Ver [Estado y limitaciones](#16-estado-y-limitaciones) |
| Aparece la pantalla de login de **LightDM** en vez del kiosco | Autologin inconsistente (problema pendiente) | Esperar; el kiosco suele aparecer tras una espera larga. **No** seleccionar `Install` |
| `tty9` en negro después de intentar la *debug shell* | Parámetro escrito con guion medio en vez de guion bajo, o no añadido a la línea `linux` del editor de GRUB | El parámetro correcto es `systemd.debug_shell=1`. Repetir: en el menú de GRUB pulsar `e`, ir al final de la línea `linux`, añadir ` systemd.debug_shell=1`, arrancar con `Ctrl+X`. Es transitorio: solo afecta a ese arranque |
| Confusión sobre el nombre del parámetro y de la unidad | El parámetro de arranque y la unidad se escriben distinto | El **parámetro de arranque del kernel** es `systemd.debug_shell=1`, con **guion bajo**. La **unidad de systemd** se llama `debug-shell.service`. Escribir ese parámetro con guion medio en lugar de guion bajo no funciona y no es equivalente: fue lo que dejó `tty9` vacío |
| El apagado espera en `Please remove the live-medium, then press ENTER` | Comportamiento normal de Debian Live al detenerse | Pulsar **Enter** en la consola de la VM |

### 16. Estado y limitaciones

- La ISO actual es un **prototipo funcional de kiosco**: arranca Debian 13,
  autentica un usuario restringido y abre el portal en Chromium a pantalla
  completa.
- **Durante esta demostración requiere que Docker Desktop y los tres servicios
  (`postgres`, `seniorcare-api`, `portal-web`) estén activos en el anfitrión.**
  El portal, la API y PostgreSQL **no** se ejecutan dentro de la ISO.
- **Todavía no es una ISO todo-en-uno / independiente.**
- **Pendiente:** mejorar el tiempo de arranque y hacer **determinista el
  autologin** (hoy a veces se queda en la pantalla de LightDM).
- Esta guía **no** afirma que el sistema esté listo para producción.
- Esta guía **no** afirma que se haya desplegado Kubernetes. Existen manifiestos
  de Kubernetes en el repositorio, pero este procedimiento usa exclusivamente
  Docker Compose y no los aplica ni los valida.
- Las limitaciones conocidas anteriores se documentan de forma explícita y no
  deben ocultarse al reproducir la demostración.

### 17. Reproducción asistida con Claude Code

Para compañeros que usen Claude Code en este procedimiento:

- Abrí Claude Code desde la **raíz del repositorio en Windows**
  (`C:\test\seniorcare-os` o la ruta equivalente de tu equipo).
- Pedile que **lea este `README.md` completo** antes de ejecutar nada.
- Ejecutá **una fase a la vez** (secciones 3 a 14) y revisá su salida antes de
  continuar.
- **Validá Git antes y después** de cada fase: `git status --short` y
  `git log -1 --oneline` deben ser los esperados.
- **No** hagas `commit`, `push` ni cambios fuera del alcance de la fase sin
  autorización explícita.
- **No** compartas secretos: contenido de `.env`, tokens, contraseñas privadas
  ni claves.
- **Detenete y reportá** cualquier diferencia de versión, ruta, hash o estado
  respecto de lo documentado aquí, en vez de improvisar una corrección.

## Documentación

- [Arquitectura](docs/arquitectura.md)
- [Autenticación y roles](docs/autenticacion-roles.md)
- [QA de arquitectura](docs/qa-arquitectura.md)
- [Seguridad](docs/seguridad.md)
- [Ejecución local](docs/ejecucion-local.md)
- [Manual de usuario](docs/manual-usuario.md)
- [Manual técnico](docs/manual-tecnico.md)
- [Alcance del MVP](docs/alcance-mvp.md)
- [Plan Scrum](docs/scrum.md)
- [Matriz de requisitos](docs/matriz-requisitos.md)
- [Diagramas UML](docs/uml/README.md)
- [Botón físico SOS por USB](docs/boton-sos-usb.md)
