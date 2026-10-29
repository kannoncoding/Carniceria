# Verificación de modelos de facturas

El proyecto de consola no tiene paquetes externos ni forma parte de `Carniceria.slnx`. Comprueba que la copia de `Factura` conserve identidades pero clone la colección y cada detalle, que los DTO de creación solo contengan campos editables y que los enums tengan los valores y etiquetas acordados.

Desde la raíz del repositorio:

```powershell
dotnet run --project .\pruebas\Fase5\Verificar-ModeloFactura.csproj
dotnet build Carniceria.slnx --no-restore --disable-build-servers -m:1 -v:minimal
```

La verificación aprobada reportó 10/10 comprobaciones y el build de solución terminó con 0 errores y 0 advertencias. No se añaden endpoints ni lógica de facturación en F5.1.
