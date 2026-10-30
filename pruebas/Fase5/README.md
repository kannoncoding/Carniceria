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
node .\pruebas\Fase5\PreparacionFactura\Verificar-SemillasHttp.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'semillas-http')
node .\pruebas\Fase5\PreparacionFactura\Verificar-PrevisualizacionHttp.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'previsualizacion-http')
node .\pruebas\Fase5\PreparacionFactura\Verificar-LimitesFacturaHttp.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'limites-factura-http')
node .\pruebas\Fase5\PreparacionFactura\Verificar-IntegridadMvc.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'integridad-mvc')
```

La validación semántica aprobó 63 verificaciones en F5.1–F5.6. La pasada final de F5.10 suma 368/368: 327 verificaciones HTTP de API (Facturas 173, Semillas/reinicio 36, Previsualización 87 y Límites 31) más 41 verificaciones MVC de las tres guardas de eliminación relacionadas. La cobertura de factura MVC queda para F7. Los 171 casos originales de F5.7 se ejecutaron antes de la semilla y se conservan como evidencia histórica; el fixture adaptado se ejecutó en F5.10, que inicia con `FAC000001` y empieza las altas de prueba en `FAC000002`. Los verificadores de identidad (8/8 con FAC000002–FAC000033) y configuración (5/5 con el almacén inyectado) también se actualizaron y se ejecutaron tras F5.8. Los smoke usan Node.js 24, sin dependencias externas, y requieren compilar la solución antes de ejecutarlos. Los builds documentados terminaron con 0 errores y 0 advertencias. Las pruebas directas invocan métodos internos de API por reflexión; el almacén de facturas es en memoria.
