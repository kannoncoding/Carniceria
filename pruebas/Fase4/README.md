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
node .\pruebas\Fase4\Verificar-Mvc.mjs --raiz . --puerto-api 5234 --puerto-web 5235 --salida $salidaMigracion
node .\pruebas\Fase4\Verificar-Mvc.mjs --grupo Rutas --raiz . --puerto-api 5234 --puerto-web 5235 --salida $salidaRutas
node .\pruebas\Fase4\Verificar-Mvc.mjs --grupo Proveedores --raiz . --puerto-api 5234 --puerto-web 5235 --salida $salidaProveedores
node .\pruebas\Fase4\Verificar-Mvc.mjs --grupo ErroresFormulario --raiz . --puerto-api 5234 --puerto-web 5235 --salida $salidaErrores
node .\pruebas\Fase4\Verificar-Mvc.mjs --grupo Mensajes --raiz . --puerto-api 5234 --puerto-web 5235 --salida $salidaMensajes
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\pruebas\Fase3\Verificar-Api.ps1 -Grupo Proveedores -Puerto 5233
```

El grupo predeterminado `Migracion` cubre operaciones MVC de los cuatro módulos, búsquedas, formularios con errores, datos duplicados, proveedor principal y restricciones de baja. `Rutas` verifica lecturas y cancelación de confirmaciones, protección antifalsificación y codificación de consultas con un receptor de prueba. El script verifica primero que ambos puertos estén libres. Puede cambiar `--puerto-api` y `--puerto-web` si están ocupados. Los registros y el resumen se escriben en el directorio temporal indicado.

Las verificaciones de F4.1 aprobaron 77/77 aserciones `Migracion`; F4.2 aprobó 77/77 `Rutas`; F4.3 aprobó 150/150 `Proveedores`; F4.4 aprobó 256/256 `ErroresFormulario`; y F4.5 aprobó 288/288 `Mensajes`, con API en 5234 y Web en 5235. F4.3 también requirió una regresión separada del grupo `Proveedores` de F3: 64/64 verificaciones en el puerto 5233. Cada grupo MVC escribe en su propio subdirectorio para conservar resúmenes y registros. Las ejecuciones aprobadas no repitieron grupos anteriores. Se probaron con Node.js 24. Los registros detallados son evidencia local y no se incluyen en el repositorio.
