# Verificación de modelos de facturas

El proyecto de consola no tiene paquetes externos ni forma parte de `Carniceria.slnx`. Comprueba que la copia de `Factura` conserve identidades pero clone la colección y cada detalle, que los DTO de creación solo contengan campos editables y que los enums tengan los valores y etiquetas acordados.

Desde la raíz del repositorio:

```powershell
dotnet restore .\pruebas\Fase5\ModeloFactura\Verificar-ModeloFactura.csproj
dotnet build .\pruebas\Fase5\ModeloFactura\Verificar-ModeloFactura.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1
dotnet run --project .\pruebas\Fase5\ModeloFactura\Verificar-ModeloFactura.csproj --no-build
dotnet restore .\pruebas\Fase5\IdentidadFactura\Verificar-IdentidadFactura.csproj
dotnet build .\pruebas\Fase5\IdentidadFactura\Verificar-IdentidadFactura.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1
dotnet run --project .\pruebas\Fase5\IdentidadFactura\Verificar-IdentidadFactura.csproj --no-build
dotnet restore .\pruebas\Fase5\ValidacionFactura\Verificar-ValidacionFactura.csproj
dotnet build .\pruebas\Fase5\ValidacionFactura\Verificar-ValidacionFactura.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1
dotnet run --project .\pruebas\Fase5\ValidacionFactura\Verificar-ValidacionFactura.csproj --no-build
dotnet restore .\pruebas\Fase5\PreparacionFactura\Verificar-PreparacionFactura.csproj
dotnet build .\pruebas\Fase5\PreparacionFactura\Verificar-PreparacionFactura.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1
dotnet run --project .\pruebas\Fase5\PreparacionFactura\Verificar-PreparacionFactura.csproj --no-build
dotnet restore .\pruebas\Fase5\CalculoFactura\Verificar-CalculoFactura.csproj
dotnet build .\pruebas\Fase5\CalculoFactura\Verificar-CalculoFactura.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1
dotnet run --project .\pruebas\Fase5\CalculoFactura\Verificar-CalculoFactura.csproj --no-build
dotnet restore .\pruebas\Fase5\ConfiguracionFactura\Verificar-ConfiguracionFactura.csproj
dotnet build .\pruebas\Fase5\ConfiguracionFactura\Verificar-ConfiguracionFactura.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1
dotnet run --project .\pruebas\Fase5\ConfiguracionFactura\Verificar-ConfiguracionFactura.csproj --no-build
dotnet build Carniceria.slnx --no-restore --disable-build-servers -m:1 -v:minimal
$carpetaEvidencia = Join-Path $env:TEMP 'pruebas-fase5'
New-Item -ItemType Directory -Force -Path $carpetaEvidencia | Out-Null
node .\pruebas\Fase5\ConfiguracionFactura\Verificar-ConfiguracionHttp.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'configuracion-http')
node .\pruebas\Fase5\PreparacionFactura\Verificar-FacturasHttp.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'facturas-http')
```

Los proyectos de consola usan carpetas `obj` y `bin` independientes y no agregan paquetes externos. F5.1 aprobó 10 comprobaciones semánticas; F5.2 aprobó 8 sobre 32 candidatos concurrentes, cultura `es-CR` y una fecha de emisión dentro de la ventana de ejecución con tolerancia de un segundo; F5.3 aprobó 17 comprobaciones de validación; F5.4 aprobó 11 comprobaciones de snapshots y disponibilidad agregada. F5.5 aprobó 12 casos de fórmula, redondeo, descuentos representables, importes redondeados a cero y errores por desbordamiento. F5.6 aprobó 5 comprobaciones semánticas y 6 por HTTP para la ruta, el tipo de contenido y los valores JSON; F5.7 aprobó 171 comprobaciones HTTP de registro, consultas, rechazo y concurrencia. Los smoke usan Node.js 24, sin dependencias externas, y requieren compilar la solución antes de ejecutarlos. Los builds documentados terminaron con 0 errores y 0 advertencias. Los verificadores internos invocan métodos de API por reflexión; el almacén de facturas es en memoria.
