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

QA aprobó 86/86 verificaciones HTTP antes de establecer la guarda global de unidad. El pago conserva los datos capturados y mantiene su rechazo defensivo con 409 si la unidad difiere. El fixture actual comprueba que la unidad de un producto facturado se rechace con 409 y luego realiza el pago con la unidad conservada.

## Edición de una factura pendiente

El verificador HTTP cubre la conservación de precios y unidades por defecto, su actualización optativa, la pertenencia de los Id de detalle, líneas nuevas o sustituidas, campos de cabecera inmutables y rechazos sin cambios parciales. Requiere Node.js 24 y la API compilada previamente.

```powershell
$carpetaEvidencia = Join-Path $env:TEMP 'fase6'
New-Item -ItemType Directory -Force -Path $carpetaEvidencia | Out-Null
node .\pruebas\Fase6\EdicionPendiente\Verificar-EdicionPendienteHttp.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'edicion-pendiente')
```

El resultado QA 150/150 corresponde a F6.3 antes de los cambios de F6.4. El fixture actual también comprueba que una edición sin cambios de una factura Pagada sea aceptada sin modificar existencias. Se acordo bloquear el cambio global de unidad mientras existan facturas vinculadas; la guarda se incorporo en F6.4.

## Edicion de una factura pagada

El grupo comprueba ediciones Pagadas con cantidades anteriores y nuevas agrupadas por producto, cambios de lineas y unidades, ajuste de existencias por diferencia, calculo, atomicidad y rechazo del cambio global de unidad para productos vinculados a facturas. Requiere Node.js 24 y la API compilada previamente.

```powershell
$carpetaEvidencia = Join-Path $env:TEMP 'fase6'
New-Item -ItemType Directory -Force -Path $carpetaEvidencia | Out-Null
node .\pruebas\Fase6\EdicionPagada\Verificar-EdicionPagadaHttp.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'edicion-pagada')
```

La pasada de F6.4 obtuvo 340/340 verificaciones: 115 de edicion Pagada, 81 de pago y 144 de edicion Pendiente. Los resultados historicos 86/86 y 150/150 son ejecuciones previas a la guarda global y no se suman a esa pasada. F6.5 verifico la guarda tambien con una factura Anulada.

## Anulacion de una factura pagada

El grupo HTTP cubre el reintegro del detalle vigente, lineas repetidas o modificadas, preservacion de datos e identidad, transiciones no permitidas, unidad global vinculada y rechazo atomico ante overflow. Requiere Node.js 24 y la API compilada previamente.

```powershell
$carpetaEvidencia = Join-Path $env:TEMP 'fase6'
New-Item -ItemType Directory -Force -Path $carpetaEvidencia | Out-Null
node .\pruebas\Fase6\AnulacionFactura\Verificar-AnulacionHttp.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'anulacion-factura')
```

QA aprobo 75/75 verificaciones HTTP; el grupo no repite las regresiones de F6.4.

## Baja de una factura pendiente

El verificador confirma la baja 204 solo para facturas Pendientes, la ausencia posterior en consultas, la conservacion de existencias y secuencia, la liberacion de referencias y los rechazos de facturas Pagadas o Anuladas. Requiere Node.js 24 y la API compilada previamente.

```powershell
$carpetaEvidencia = Join-Path $env:TEMP 'fase6'
New-Item -ItemType Directory -Force -Path $carpetaEvidencia | Out-Null
node .\pruebas\Fase6\EliminacionFactura\Verificar-EliminacionHttp.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'eliminacion-factura')
```

QA aprobo 55/55 verificaciones HTTP; las bajas Pagadas y Anuladas devolvieron 409.

## Integridad de relaciones

El grupo verifica las cuatro guardas de bajas, el bloqueo de cambio de unidad en los tres estados de factura, bajas libres y carreras acotadas de alta/baja y edicion/baja. Requiere Node.js 24 y la API compilada previamente.

```powershell
$carpetaEvidencia = Join-Path $env:TEMP 'fase6'
New-Item -ItemType Directory -Force -Path $carpetaEvidencia | Out-Null
node .\pruebas\Fase6\Integridad\Verificar-IntegridadHttp.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'integridad-relaciones')
```

QA aprobo 174/174 verificaciones. En las 12 carreras acotadas se observo primero la baja, que termino en 204, y luego se rechazo la escritura relacionada; no se observaron referencias huerfanas ni mutaciones parciales. Esta muestra no demuestra formalmente ambos ordenes concurrentes.

## Atomicidad de calculo

La auditoria de `ActualizarFactura`, `PagarFactura` y `AnularFactura` confirma que los candidatos y stocks se preparan antes de escribir. El grupo HTTP fuerza un desbordamiento de calculo despues de preparar un delta valido para una factura Pagada y comprueba que factura, productos, existencias y secuencia no cambien. Requiere Node.js 24 y la API compilada previamente.

```powershell
$carpetaEvidencia = Join-Path $env:TEMP 'fase6'
New-Item -ItemType Directory -Force -Path $carpetaEvidencia | Out-Null
node .\pruebas\Fase6\Atomicidad\Verificar-AtomicidadHttp.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'atomicidad')
```

QA aprobo 28/28 verificaciones; el desbordamiento controlado devolvio 400, preservo los datos y la siguiente factura recibio FAC000003. No se repitieron los grupos anteriores.

## Concurrencia y repeticion

El grupo observa pagos y anulaciones repetidos o concurrentes, ediciones Pagadas sin cambios y dos facturas Pendientes que compiten por las mismas existencias. Requiere Node.js 24 y la API compilada previamente.

```powershell
$carpetaEvidencia = Join-Path $env:TEMP 'fase6'
New-Item -ItemType Directory -Force -Path $carpetaEvidencia | Out-Null
node .\pruebas\Fase6\Concurrencia\Verificar-ConcurrenciaHttp.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'concurrencia')
```

QA aprobo 45/45 verificaciones. Las carreras probaron cuatro situaciones y observaron resultados coherentes sin sobreventa ni doble ajuste; la muestra no garantiza todos los ordenes posibles de ejecucion.
