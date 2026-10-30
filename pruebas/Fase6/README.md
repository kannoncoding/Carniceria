# Verificacion de la politica de facturas

El proyecto comprueba las cuatro operaciones contra los tres estados de factura, rechaza valores de enum no definidos y verifica que la politica no cambie estado ni existencias. No agrega paquetes ni ejecuta operaciones del almacen.

Desde la raiz del repositorio:

```powershell
dotnet restore .\pruebas\Fase6\PoliticaFactura\Verificar-PoliticaFactura.csproj
dotnet build .\pruebas\Fase6\PoliticaFactura\Verificar-PoliticaFactura.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1
dotnet run --project .\pruebas\Fase6\PoliticaFactura\Verificar-PoliticaFactura.csproj --no-build
```
