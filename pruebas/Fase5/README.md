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
dotnet build Carniceria.slnx --no-restore --disable-build-servers -m:1 -v:minimal
```

Los proyectos de consola usan carpetas `obj` y `bin` independientes y no agregan paquetes externos. F5.1 aprobó 10 comprobaciones semánticas; F5.2 aprobó 8 sobre 32 candidatos concurrentes, cultura `es-CR` y una fecha de emisión dentro de la ventana de ejecución con tolerancia de un segundo; F5.3 aprobó 17 comprobaciones de validación. Los builds documentados terminaron con 0 errores y 0 advertencias. Los verificadores de identidad y validación invocan métodos internos de API por reflexión; todavía no hay endpoint de factura ni registro persistente.
