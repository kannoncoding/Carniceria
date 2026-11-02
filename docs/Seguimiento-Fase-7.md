# Seguimiento de la fase 7

Fase 7 integra la interfaz MVC de facturas con la API. El primer incremento ofrece consultas y confirmaciones reales de las transiciones disponibles; el registro y la edicion se completaran en pasos posteriores.

| Paso | Estado | Alcance |
| --- | --- | --- |
| F7.1 | Parcial | Se agregaron navegacion, listado, detalle y busqueda de facturas desde la API. Las confirmaciones GET son de solo lectura; los POST protegidos solicitan pago, anulacion o eliminacion segun el estado actual. Se valida la respuesta, se conserva el flujo PRG y se manejan conflictos, faltantes y fallas de transporte. Ante una respuesta de pago incierta, el controlador consulta primero el estado y no reintenta automaticamente; el flujo se verifico con un proxy en la prueba HTTP MVC. Build de solucion: 0 errores y 0 advertencias. QA aprobo 83/83 verificaciones. La revision visual en navegador queda pendiente. Crear y Editar no se ofrecen hasta que sus flujos esten completos. |

## Prerequisito para el calculo de edicion

El prerequisito API de F7.5 esta completado: `POST /api/facturas/{numero}/calcular` recibe `SolicitudEdicionFactura` y devuelve `RespuestaCalculoFactura`. Comparte con la actualizacion la validacion, los valores historicos u optativos, el control de existencias y el calculo. La vista previa no reemplaza la factura ni aplica existencias, y la respuesta no expone identidades de lineas. La compilacion de solucion termino con 0 errores y 0 advertencias. QA aprobo 363/363 verificaciones: 76 del calculo de edicion, 144 de actualizacion Pendiente, 115 de actualizacion Pagada y 28 de atomicidad. Este hito de API prepara el calculo MVC, pero no completa los formularios Crear ni Editar ni el paso F7.5 en MVC.
## Limites del incremento

- Los importes, estados y detalles mostrados provienen de `RespuestaFactura`; MVC no recalcula ni modifica el inventario.
- La API es autoridad para las transiciones. GET de confirmacion no muta; los POST protegidos son la unica solicitud de cambio.
- La recuperacion de una respuesta de pago perdida ya consulta el estado actual y evita el reintento automatico; la verificacion HTTP MVC paso. La revision visual en navegador queda pendiente.
- La casilla `ActualizarPreciosYUnidades` y los formularios Crear/Editar se incorporaran con sus flujos funcionales posteriores.
- Los pasos restantes de F7 siguen pendientes segun el plan del proyecto.
