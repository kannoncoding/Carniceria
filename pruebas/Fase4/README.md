# Verificación MVC de F4

`Verificar-Mvc.mjs` usa solo módulos incluidos con Node.js. Inicia procesos propios de API y Web en puertos aislados, envía formularios con cookie y token antifalsificación y termina ambos procesos al cerrar. No compila ni restaura proyectos; prepara los binarios antes de ejecutarlo.

Desde la raíz del repositorio, prepara los binarios:

```powershell
dotnet restore Carniceria.slnx
dotnet build Carniceria.slnx --no-restore --disable-build-servers -m:1 -v:minimal
$salida = Join-Path $env:TEMP 'carniceria-fase4'
node .\pruebas\Fase4\Verificar-Mvc.mjs --raiz . --puerto-api 5234 --puerto-web 5235 --salida $salida
```

El grupo cubre operaciones MVC de los cuatro módulos, búsquedas, formularios con errores, datos duplicados, proveedor principal, restricciones de baja y rutas inexistentes. El script verifica primero que ambos puertos estén libres. Puede cambiar `--puerto-api` y `--puerto-web` si están ocupados. Los registros y el resumen se escriben en el directorio temporal indicado.

La ejecución inicial de F4 aprobó 77/77 aserciones con API en 5234 y Web en 5235. Una prueba de cierre aislada corrigió el manejo de terminación del harness; no requirió cambios de aplicación ni repetir las aserciones aprobadas. Los resultados detallados de esa ejecución se conservaron fuera del repositorio y no se incluyen aquí.
