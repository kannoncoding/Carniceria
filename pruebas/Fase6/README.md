# Verificacion de Fase 6

Los verificadores no agregan paquetes externos.

## Politica de transiciones

Comprueba las cuatro operaciones contra los tres estados de factura, rechaza enums no definidos y verifica los mensajes de conflicto y que la politica no cambie estado ni existencias. Ejecuta 14 casos con 26 aserciones.

Desde la raiz del repositorio:

```powershell
dotnet restore .\pruebas\Fase6\PoliticaFactura\Verificar-PoliticaFactura.csproj
dotnet build .\pruebas\Fase6\PoliticaFactura\Verificar-PoliticaFactura.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1
dotnet run --project .\pruebas\Fase6\PoliticaFactura\Verificar-PoliticaFactura.csproj --no-build
```

## Pago de una factura

El smoke HTTP inicia la API en un puerto aislado y cubre el pago, la factura ya pagada, los conflictos de stock, las unidades incompatibles y el rechazo completo sin mutaciones parciales. Requiere Node.js 24 y la API compilada previamente; el script no compila los proyectos.

```powershell
$carpetaEvidencia = Join-Path $env:TEMP 'fase6'
New-Item -ItemType Directory -Force -Path $carpetaEvidencia | Out-Null
node .\pruebas\Fase6\PagoFactura\Verificar-PagoHttp.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'pago-factura')
```

QA aprobó 86/86 verificaciones HTTP. El pago mantiene los datos capturados en la factura y rechaza con 409 si la unidad actual de un producto difiere de la unidad guardada. Esa comprobación no resuelve la política global para cambiar la unidad de un producto facturado.
