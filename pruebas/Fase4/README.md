# Verificación MVC de F4

`Verificar-Mvc.mjs` usa solo módulos incluidos con Node.js. Inicia procesos propios de API y Web en puertos aislados, envía formularios con cookie y token antifalsificación y termina ambos procesos al cerrar. No compila ni restaura proyectos; prepara los binarios antes de ejecutarlo.

Desde la raíz del repositorio, prepara los binarios y ejecuta cada grupo por separado:

```powershell
dotnet restore Carniceria.slnx
dotnet build Carniceria.slnx --no-restore --disable-build-servers -m:1 -v:minimal
$salida = Join-Path $env:TEMP 'carniceria-fase4'
$salidaMigracion = Join-Path $salida 'Migracion'
$salidaRutas = Join-Path $salida 'Rutas'
$salidaProveedores = Join-Path $salida 'Proveedores'
$salidaErrores = Join-Path $salida 'ErroresFormulario'
$salidaMensajes = Join-Path $salida 'Mensajes'
$salidaTransportes = Join-Path $salida 'Transportes'
$salidaInterfaz = Join-Path $salida 'Interfaz'
$salidaFechas = Join-Path $salida 'Fechas'
node .\pruebas\Fase4\Verificar-Mvc.mjs --raiz . --puerto-api 5234 --puerto-web 5235 --salida $salidaMigracion
node .\pruebas\Fase4\Verificar-Mvc.mjs --grupo Rutas --raiz . --puerto-api 5234 --puerto-web 5235 --salida $salidaRutas
node .\pruebas\Fase4\Verificar-Mvc.mjs --grupo Proveedores --raiz . --puerto-api 5234 --puerto-web 5235 --salida $salidaProveedores
node .\pruebas\Fase4\Verificar-Mvc.mjs --grupo ErroresFormulario --raiz . --puerto-api 5234 --puerto-web 5235 --salida $salidaErrores
node .\pruebas\Fase4\Verificar-Mvc.mjs --grupo Mensajes --raiz . --puerto-api 5234 --puerto-web 5235 --salida $salidaMensajes
node .\pruebas\Fase4\Verificar-Mvc.mjs --grupo Transportes --raiz . --puerto-api 5234 --puerto-web 5235 --salida $salidaTransportes
node .\pruebas\Fase4\Verificar-Interfaz.mjs --raiz . --salida $salidaInterfaz
node .\pruebas\Fase4\Verificar-Fechas.mjs --raiz . --puerto-api 5234 --puerto-web 5235 --salida $salidaFechas
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\pruebas\Fase3\Verificar-Api.ps1 -Grupo Proveedores -Puerto 5233
```

El grupo predeterminado `Migracion` cubre operaciones MVC de los cuatro módulos, búsquedas, formularios con errores, datos duplicados, proveedor principal y restricciones de baja. `Rutas` verifica lecturas y cancelación de confirmaciones, protección antifalsificación y codificación de consultas con un receptor de prueba. El script verifica primero que ambos puertos estén libres. Puede cambiar `--puerto-api` y `--puerto-web` si están ocupados. Los registros y el resumen se escriben en el directorio temporal indicado.

`Verificar-Interfaz.mjs` reserva los puertos 5234 (API), 5235 (Web), 5236 (proxy) y 5237 (CDP); usa un perfil temporal propio que conserva para diagnóstico y cierra el navegador y procesos que inició. Si esos puertos están ocupados, se pueden cambiar con `--puerto-api`, `--puerto-web`, `--puerto-proxy` y `--puerto-depuracion`. El script busca Edge o Chrome en sus ubicaciones estándar; se puede indicar otro ejecutable con `--navegador`. Las capturas y registros resultantes se guardan en la carpeta de salida y no se versionan.

Las verificaciones aprobaron 77/77 `Migracion`, 77/77 `Rutas`, 150/150 `Proveedores`, 256/256 `ErroresFormulario`, 288/288 `Mensajes`, 151/151 `Transportes`, 83/83 `Interfaz` y 52/52 `Fechas`. Los grupos MVC se ejecutaron en puertos aislados y escriben en subdirectorios distintos. `Interfaz` requiere Edge o Chrome local, Node.js 24 y CDP; `Fechas` usa HTTP MVC. La regresión del grupo `Proveedores` de F3 fue separada: 64/64 en el puerto 5233. Las ejecuciones aprobadas no repitieron grupos anteriores. Los registros detallados son evidencia local y no se incluyen en el repositorio.
