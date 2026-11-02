# Verificacion de Fase 7

Requisitos: .NET 10, Node.js 24 y la solucion compilada. Los verificadores Node no restauran ni compilan proyectos. Desde la raiz del repositorio, prepara los binarios y una carpeta de salida temporal:

```powershell
dotnet restore .\Carniceria.slnx
dotnet build .\Carniceria.slnx --no-restore --disable-build-servers -m:1 -v:minimal
$carpetaEvidencia = Join-Path $env:TEMP 'fase7'
New-Item -ItemType Directory -Force -Path $carpetaEvidencia | Out-Null
```

## Formularios de factura

El grupo `Verificar-FormulariosMvc.mjs` inicia API, MVC y un proxy en puertos aislados. Cubre creación y edición, catálogos, filas con claves estables, preservación de valores y errores, cálculos de creación y edición, valores históricos y optativos, y campos de servidor no confiables.

```powershell
node .\pruebas\Fase7\Verificar-FormulariosMvc.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'formularios')
```

QA aprobó **89/89 verificaciones**. La prueba confirmó que la previsualización no modifica factura, existencias ni secuencia, y que los formularios conservan los Id de detalle del registro editado.

## Consultas y transiciones MVC

El grupo `Verificar-ConsultasTransicionesMvc.mjs` verifica menú, listado, detalle, búsqueda codificada, confirmaciones GET sin mutación, antifalsificación, pago, anulación y eliminación con PRG, errores del servicio y recuperación ante respuesta de pago perdida.

```powershell
node .\pruebas\Fase7\Verificar-ConsultasTransicionesMvc.mjs --raiz . --salida (Join-Path $carpetaEvidencia 'consultas-transiciones')
```

QA aprobó **83/83 verificaciones** en la regresión adaptada: Crear y Editar están disponibles y los flujos anteriores se conservan. Una ejecución diagnóstica parcial de 48 casos no se cuenta.

## Resultado actual

Los grupos actuales suman **172/172 verificaciones HTTP** (89 + 83). La compilación de la solución terminó con 0 errores y 0 advertencias. La comprobación del prerequisito de cálculo de edición en API fue una ejecución separada de **363/363** y no se suma al total MVC.

Las pruebas inician y cierran sus propios procesos. Los puertos predeterminados son API 5234, MVC 5235 y proxy 5236; cada script admite opciones para cambiarlos. Guarda cada ejecución en su propia subcarpeta para conservar sus resúmenes.

## Correspondencia con la lista F7 del plan

Los diez pasos de la lista F7 están completados en el alcance HTTP probado. F7.6 muestra los importes y conserva decimales; F7.7 cubre búsqueda independiente y resultados vacíos; F7.8 verifica confirmaciones y cantidades afectadas; F7.9 verifica la conservación del formulario ante errores; y F7.10 verifica antifalsificación y ausencia de cambios mediante GET. Las evidencias corresponden a los grupos actuales de 89 formularios y 83 consultas/transiciones; el grupo API de 363 verificaciones es independiente y no se agrega al total MVC.

La revisión visual en navegador sigue pendiente de ejecución manual. La regresión global F8 cubre módulos y escenarios adicionales fuera de esta verificación de F7.
