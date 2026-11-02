# Regresión de fase 8

F8 ejecuta las suites actuales disponibles para cubrir T02–T30, agrega un escenario de fallo real de API tras abrir formularios y verifica un candidato limpio para T31–T32. T01 se registra como confirmación manual del usuario. La matriz en `docs/Seguimiento-Fase-8.md` separa grupos de ejecución y evidencia.

## Formularios obsoletos y reinicio de API

`Verificar-FallosReinicioMvc.mjs` inicia API, MVC y proxy propios; comprueba lista vacía, API apagada durante un envío, preservación de valores y opciones, reinicio que genera IDs nuevos, alta con referencias antiguas y edición de una factura ya ausente. No restaura ni compila proyectos. Requiere .NET 10 y Node.js 24.

Desde la raíz del repositorio, compila primero y ejecuta el verificador:

```powershell
dotnet build .\Carniceria.slnx --no-restore --disable-build-servers -m:1 -v:minimal
$carpetaEvidencia = Join-Path $env:TEMP 'fase8'
New-Item -ItemType Directory -Force -Path $carpetaEvidencia | Out-Null
node .\pruebas\Fase8\Verificar-FallosReinicioMvc.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'fallos-reinicio')
```

QA aprobó **13/13 verificaciones**. El verificador usa los puertos 5234, 5235 y 5236; admite `--puerto-api`, `--puerto-web` y `--puerto-proxy` para cambiarlos. Cierra solo los procesos propios y confirma que los puertos queden libres.

## Regresión actual T01–T32

La corrida F8 aprobó 2 249 verificaciones en grupos separados: F3 API 857, F5 API 333, F6 API 739, F4 MVC reinicios 58 y migración 77, F7 MVC formularios 89, smoke del candidato T31 83 y escenarios F8.4 13. Los grupos no se fusionan en una única suite. T01 se reporta como confirmación manual del usuario después del inicio dual, no como ejecución de QA en F8.

F8.5 quedó revisado estáticamente: las vistas y JavaScript no llaman la API directamente, las transiciones de inventario pertenecen a API y los proyectos no agregan dependencias de persistencia. F8.6 aprobó 19/19 comprobaciones en Edge emulado para escritorio a 1440 px y móvil a 390 px; no se probó un dispositivo físico. Consulta el [seguimiento de F8](../../docs/Seguimiento-Fase-8.md).

## Revisión visual de facturas

`Verificar-InterfazFacturas.mjs` inicia API, Web, proxy y Edge headless propios. Desde la raíz, compila Web y ejecuta la revisión:

```powershell
dotnet build .\Carniceria.Web\Carniceria.Web.csproj
$carpetaEvidencia = Join-Path $env:TEMP 'fase8-facturas'
New-Item -ItemType Directory -Force -Path $carpetaEvidencia | Out-Null
node .\pruebas\Fase8\Verificar-InterfazFacturas.mjs --raiz . --salida $carpetaEvidencia
```

La pasada QA aprobó **19/19** en escritorio 1440 × 1000, móvil emulado 390 × 844 y zoom CSS 200 %. Verificó que la tabla se desplace dentro de su contenedor sin ampliar la ventana móvil, además de etiquetas, teclado, cálculos, validación, confirmación y ausencia de llamadas API directas desde el navegador. Se requiere Node.js y Edge o Chrome instalados. La emulación no sustituye una revisión en dispositivo físico.

T31–T32 se comprobaron en F8 sobre un ZIP candidato técnico, no final. La compilación y el smoke del candidato se describen en el seguimiento. F10 debe validar de nuevo el paquete definitivo.
