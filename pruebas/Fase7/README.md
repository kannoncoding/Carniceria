# Verificacion de Fase 7

## Consultas y transiciones MVC

El verificador inicia la API, el sitio MVC y un proxy en puertos aislados. Comprueba menu, listado, detalle, busqueda codificada, confirmaciones GET sin mutacion, antifalsificacion, pago/anulacion/baja con PRG, errores del servicio, cantidades y descuentos decimales, y respuesta de pago perdida con consulta posterior del estado.

Requiere .NET 10, Node.js 24 y los binarios de API y Web compilados. El script no restaura ni compila proyectos. Desde la raiz del repositorio:

```powershell
dotnet restore .\Carniceria.slnx
dotnet build .\Carniceria.slnx --no-restore --disable-build-servers -m:1 -v:minimal
$carpetaEvidencia = Join-Path $env:TEMP 'fase7'
New-Item -ItemType Directory -Force -Path $carpetaEvidencia | Out-Null
node .\pruebas\Fase7\Verificar-ConsultasTransicionesMvc.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'consultas-transiciones')
```

QA aprobo **83/83 verificaciones**. El grupo confirma 404 y errores controlados, no repite mutaciones al abrir GET, rechaza POST sin antifalsificacion y conserva los importes y cantidades de la API. Ante una respuesta de pago perdida, observa un solo POST, consulta el estado y muestra el resultado confirmado. Los puertos de API, MVC y proxy son 5234, 5235 y 5236; pueden cambiarse con `--puerto-api`, `--puerto-web` y `--puerto-proxy`.

Crear, Editar y la casilla `ActualizarPreciosYUnidades` siguen pendientes. La revision visual del sitio en navegador tambien queda pendiente; el verificador ejercita el flujo MVC mediante HTTP.
