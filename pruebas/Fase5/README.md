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
dotnet build Carniceria.slnx --no-restore --disable-build-servers -m:1 -v:minimal
```

Los proyectos de consola usan carpetas `obj` y `bin` independientes y no agregan paquetes externos. F5.1 aprobó 10 comprobaciones semánticas; F5.2 aprobó 8 sobre 32 candidatos concurrentes, cultura `es-CR` y una fecha de emisión dentro de la ventana de ejecución con tolerancia de un segundo. El build de solución terminó con 0 errores y 0 advertencias. El verificador de identidad invoca el preparador interno de API por reflexión; no hay endpoint de factura ni registro persistente en F5.1/F5.2.
