# Verificación reproducible de la API

## Requisitos

- Windows PowerShell 5.1 o PowerShell compatible con Windows.
- SDK de .NET 10 instalado.
- Ejecutar desde la raíz del repositorio.
- Puerto 5233 libre. Cada grupo inicia una instancia aislada de la API con datos en memoria y la detiene en `finally`.

Restaure dependencias y compile la solución antes de ejecutar los grupos:

```powershell
dotnet restore Carniceria.slnx
dotnet build Carniceria.slnx --no-restore --disable-build-servers -m:1 -v:minimal
```

El script usa únicamente PowerShell, .NET y la API del proyecto; no instala plugins ni paquetes. Está guardado con BOM UTF-8 para compatibilidad con Windows PowerShell 5.1.

## Grupos

Ejecute cada grupo por separado desde la raíz del repositorio:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\pruebas\Fase3\Verificar-Api.ps1 -Grupo Validaciones -Puerto 5233
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\pruebas\Fase3\Verificar-Api.ps1 -Grupo Busquedas -Puerto 5233
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\pruebas\Fase3\Verificar-Api.ps1 -Grupo Proveedores -Puerto 5233
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\pruebas\Fase3\Verificar-Api.ps1 -Grupo Integridad -Puerto 5233
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\pruebas\Fase3\Verificar-Api.ps1 -Grupo Concurrencia -Puerto 5233
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\pruebas\Fase3\Verificar-Api.ps1 -Grupo Errores -Puerto 5233
```

El script verifica que el puerto esté libre, inicia la DLL compilada con una URL local, espera el endpoint de estado, ejecuta únicamente el grupo solicitado y cierra el proceso que inició. Si el puerto ya está ocupado o no existe una compilación, termina con un error explicativo; no detenga procesos ajenos para liberar el puerto.

## Alcance

Los grupos cubren validaciones, búsquedas, referencias de productos a proveedores, integridad al eliminar proveedores, intercalaciones concurrentes observadas y respuestas HTTP de error. Las carreras son una muestra reproducible, no una demostración formal de todas las ejecuciones posibles. F3 verifica la superficie API; la migración del almacén MVC corresponde a F4 y las relaciones con facturas a F6.
