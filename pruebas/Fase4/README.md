# Verificación MVC de F4

`Verificar-Mvc.mjs` usa solo módulos incluidos con Node.js. Inicia procesos propios de API y Web en puertos aislados, envía formularios con cookie y token antifalsificación y termina ambos procesos al cerrar. No compila ni restaura proyectos; prepara los binarios antes de ejecutarlo.

Desde la raíz del repositorio, prepara los binarios y ejecuta cada grupo por separado:

```powershell
dotnet restore Carniceria.slnx
dotnet build Carniceria.slnx --no-restore --disable-build-servers -m:1 -v:minimal
$salida = Join-Path $env:TEMP 'carniceria-fase4'
$salidaMigracion = Join-Path $salida 'Migracion'
$salidaRutas = Join-Path $salida 'Rutas'
node .\pruebas\Fase4\Verificar-Mvc.mjs --raiz . --puerto-api 5234 --puerto-web 5235 --salida $salidaMigracion
node .\pruebas\Fase4\Verificar-Mvc.mjs --grupo Rutas --raiz . --puerto-api 5234 --puerto-web 5235 --salida $salidaRutas
```

El grupo predeterminado `Migracion` cubre operaciones MVC de los cuatro módulos, búsquedas, formularios con errores, datos duplicados, proveedor principal y restricciones de baja. `Rutas` verifica lecturas y cancelación de confirmaciones, protección antifalsificación y codificación de consultas con un receptor de prueba. El script verifica primero que ambos puertos estén libres. Puede cambiar `--puerto-api` y `--puerto-web` si están ocupados. Los registros y el resumen se escriben en el directorio temporal indicado.

Las verificaciones de F4.1 aprobaron 77/77 aserciones `Migracion`; F4.2 aprobó otras 77/77 aserciones `Rutas`, con API en 5234 y Web en 5235. Una prueba de cierre aislada corrigió el manejo de terminación del harness; no requirió cambios de aplicación ni repetir las aserciones ya aprobadas. Los resultados detallados se conservaron fuera del repositorio y no se incluyen aquí.
