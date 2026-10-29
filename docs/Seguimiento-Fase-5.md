# Seguimiento de la fase 5

Registro incremental del modelo y los flujos de facturas. La fase sigue abierta; F5.1 solo define entidades y contratos compartidos.

| Paso | Estado | Evidencia o límite |
| --- | --- | --- |
| F5.1 | Completado | Se añadieron `Factura`, `DetalleFactura`, `MetodoPago`, `EstadoFactura` y los contratos de solicitud/respuesta. `Factura.Copiar()` clona la colección y cada detalle, conservando sus identificadores; las solicitudes de creación contienen solo ClienteId, EmpleadoId, método, descuento, producto y cantidad. QA aprobó la revisión estática sin defectos. El verificador semántico aprobó 10/10 comprobaciones de copias profundas, separación de campos de entrada y catálogos. El build de `Carniceria.slnx` terminó con 0 errores y 0 advertencias. Instrucciones: [`README.md`](../pruebas/Fase5/README.md). |
| F5.2 | Pendiente | Generar número, emisión e Id de detalles en API; forzar estado inicial Pendiente y excluir esos campos de las solicitudes. |
| F5.3 | Pendiente | Validar relaciones, descuento 0–25, método permitido, líneas y cantidades; validar valores de enum recibidos por JSON. |
| F5.4 | Pendiente | Obtener precios y unidades desde productos, agrupar demanda por producto y comprobar disponibilidad total. |
| F5.5 | Pendiente | Implementar cálculo único con decimal, IVA del 13 % y redondeo documentado. |
| F5.6 | Pendiente | Centralizar el descuento máximo en configuración de API y exponerlo para presentación. |
| F5.7 | Pendiente | Implementar crear, listar, consultar y buscar facturas por número o identificación del cliente. |
| F5.8 | Pendiente | Añadir una factura Pendiente a las semillas con dos líneas válidas, sin descontar inventario. |
| F5.9 | Pendiente | Implementar el cálculo sin persistencia si MVC incorpora previsualización; no reservar número ni stock. |
| F5.10 | Pendiente | Verificar el ejemplo CRC 11 187, descuentos 0/25, valores negativos y manipulación de campos calculados. |

## Decisiones de implementación

- Los importes usan `decimal`; cada importe monetario se redondea a dos decimales con `AwayFromZero`, y el subtotal suma las líneas redondeadas. No se imponen enteros ni precisión máxima a las cantidades.
- La emisión usa `DateTimeOffset`, hora real del servidor convertida a zona de Costa Rica y presentación de 24 horas `dd/MM/yyyy HH:mm:ss`.
- El número usa prefijo `FAC` más secuencia numérica generada bajo el mismo bloqueo del almacén. Los Id de líneas son Guid asignados por API; al reiniciar se reconstruye solo la semilla.
- No se implementan pago, anulación ni cambios de inventario en F5. El histórico de precios/unidades y la regla de cambio de unidad se resuelven antes de cerrar F6.

## Alcance actual

F5.1 no añade endpoints, almacenes, cálculo, persistencia de facturas, validación de negocio ni cambios a Web. Las pruebas de F5.1 comprueban aislamiento de copias, identidad de detalles y separación entre campos editables y campos asignados/calculados por el servidor.
