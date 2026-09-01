# Aplicación móvil SeniorCare

## Android desde Windows

1. Ejecutar `INSTALAR_ANDROID.cmd` desde la raíz del proyecto.
2. Cerrar y abrir nuevamente VS Code.
3. Abrir `SeniorCare.Mobile.csproj`.
4. Iniciar primero la plataforma con `INICIAR_SENIORCARE.cmd`.
5. Seleccionar un emulador Android.
6. Ejecutar el proyecto.

El emulador Android utiliza `http://10.0.2.2:8088` para llegar a Docker en
Windows.

## Teléfono Android físico

En `Services/SeniorCareApiClient.cs`, sustituir la dirección base por la IP
local de la computadora, por ejemplo:

```text
http://192.168.1.20:8088
```

El teléfono y la computadora deben estar en la misma red.

## iOS

El proyecto habilita el destino `net10.0-ios` automáticamente en macOS. Para
compilarlo desde Windows se necesita una Mac con Xcode enlazada y la propiedad
`BuildIosOnWindows=true`.

Las conexiones HTTP se habilitan únicamente para desarrollo local. El
despliegue final debe utilizar HTTPS.
