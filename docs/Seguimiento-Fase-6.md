# Seguimiento de la fase 6

Fase 6 centraliza y prueba las transiciones de estado de factura y su efecto atomico sobre las existencias. Cada paso se completa con evidencia revisada; la interfaz de facturas corresponde a F7.

| Paso | Estado | Alcance |
| --- | --- | --- |
| F6.1 | Completado | Regla unica de transiciones en API: editar Pendiente/Pagada; pagar Pendiente; anular Pagada; eliminar Pendiente. Las combinaciones restantes y los enums no definidos devuelven conflicto 409. QA aprobo 14 casos y 26 aserciones, incluidos mensajes/campos de conflicto y ausencia de mutacion. Build de solucion: 0 errores y 0 advertencias. No implementa operaciones ni cambia existencias. |
| F6.2 | Pendiente | Pagar una factura Pendiente, revalidar la demanda total y descontar todo el stock bajo el bloqueo comun. |
| F6.3 | Pendiente | Editar factura Pendiente y recalcular sin cambiar stock; conservar numero y emision. |
| F6.4 | Pendiente | Editar factura Pagada con ajuste atomico de cantidades anteriores y nuevas. |
| F6.5 | Pendiente | Anular una factura Pagada, reintegrar el detalle vigente una sola vez. |
| F6.6 | Pendiente | Permitir baja solo de facturas Pendientes y rechazar cambios de facturas Anuladas o bajas de Pagadas. |
| F6.7 | Pendiente | Completar las guardas de eliminacion por relaciones para los tres estados. |
| F6.8 | Pendiente | Preparar todos los cambios y rechazos antes de mutar factura, detalles o stock. |
| F6.9 | Pendiente | Comprobar repeticiones, concurrencia y edicion sin cambios sin efectos parciales. |
| F6.10 | Pendiente | Definir el flujo de consulta tras una respuesta de pago incierta y evitar reintentos mutadores automaticos. |

## Decisiones y limites

- F6.1 se puede implementar sin resolver el historico de precios/unidades ni el cambio de unidad de productos facturados.
- La politica de F6.1 vive en API y no acepta un estado destino arbitrario. Los endpoints y operaciones de pago, edicion y anulacion se incorporan en sus pasos posteriores.
- Comentarios de codigo nuevos usan espanol sin tildes; los textos visibles conservan su ortografia normal.
- Historico de precio/unidad y cambio de unidad siguen pendientes de confirmacion antes de implementar operaciones que dependan de esas reglas.
