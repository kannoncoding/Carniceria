# Seguimiento de la fase 5

Registro incremental del modelo y los flujos de facturas. La fase sigue abierta; F5.1 solo define entidades y contratos compartidos.

| Paso | Estado | Evidencia o límite |
| --- | --- | --- |
| F5.1 | Completado | Se añadieron `Factura`, `DetalleFactura`, `MetodoPago`, `EstadoFactura` y los contratos de solicitud/respuesta. `Factura.Copiar()` clona la colección y cada detalle, conservando sus identificadores; las solicitudes de creación contienen solo ClienteId, EmpleadoId, método, descuento, producto y cantidad. QA aprobó la revisión estática sin defectos. El verificador semántico aprobó 10/10 comprobaciones de copias profundas, separación de campos de entrada y catálogos. El build de `Carniceria.slnx` terminó con 0 errores y 0 advertencias. Instrucciones: [`README.md`](../pruebas/Fase5/README.md). |
| F5.2 | Completado | Se añadió al almacén API una preparación interna de candidato validado bajo `_sincronizacion`: número `FAC` con seis dígitos y `InvariantCulture`, emisión actual convertida a `America/Costa_Rica` como `DateTimeOffset`, estado `Pendiente` e Id nuevos para las líneas. El preparador devuelve una copia completa, no la guarda ni expone endpoint; la secuencia no se consume durante cálculos o previsualización. QA aprobó 8/8 comprobaciones, incluidas 32 candidaturas simultáneas bajo cultura `es-CR` y fecha dentro de la ventana de ejecución. El build de `Carniceria.slnx` terminó con 0 errores y 0 advertencias. Ver [`Verificar-IdentidadFactura.csproj`](../pruebas/Fase5/IdentidadFactura/Verificar-IdentidadFactura.csproj). |
| F5.3 | Completado | Se añadió la regla central del descuento máximo (25 %) y una validación interna de la solicitud que acumula errores de cabecera y de cada línea. Comprueba clientes, empleados y productos existentes bajo el bloqueo del almacén; rechaza valores de enum indefinidos, descuento fuera de 0–25, listas nulas o vacías, líneas nulas y cantidades no positivas. No cambia el candidato, el almacén ni las existencias. QA aprobó el verificador acotado con 17/17 comprobaciones; la compilación de API y modelos terminó con 0 errores y 0 advertencias. Sin endpoint, cálculo de precio ni validación de existencias, que corresponden a pasos posteriores. |
| F5.4 | Completado | Se añadieron líneas candidatas con precio y unidad copiados de productos del almacén bajo `_sincronizacion`. La demanda se agrupa por producto, incluidas líneas repetidas; si el total supera las existencias se devuelve `ConflictoIntegridad` (409), sin líneas parciales. La suma decimal desbordada se informa como error de validación (400). Los snapshots no alteran productos, existencias ni secuencia de factura. QA aprobó el verificador dirigido con 11/11 comprobaciones; la compilación de API y modelos terminó con 0 errores y 0 advertencias. No se guardan facturas ni se expone endpoint. |
| F5.5 | Completado | Se añadió un calculador único que opera sobre una copia y usa `decimal`: redondea cada línea a dos decimales con `AwayFromZero`, suma líneas redondeadas y calcula descuento como `Subtotal / 100 * porcentaje`, base, IVA del 13 % y total con la misma regla. Los desbordamientos se convierten en errores de validación por campo; no se exige un importe mínimo, por lo que un total redondeado a cero es aceptado. QA aprobó el verificador dirigido con 12/12, incluido el ejemplo de CRC 11 187, descuentos 0/25, puntos medios, subtotal alto con descuento representable, porcentaje decimal diminuto y desbordamientos. |
| F5.6 | Pendiente | Centralizar el descuento máximo en configuración de API y exponerlo para presentación. |
| F5.7 | Pendiente | Validar el candidato antes de asignar número; generar identidad y persistir bajo el mismo bloqueo. Implementar crear, listar, consultar y buscar facturas por número o identificación del cliente. |
| F5.8 | Pendiente | Añadir una factura Pendiente a las semillas con dos líneas válidas, sin descontar inventario. |
| F5.9 | Pendiente | Implementar el cálculo sin persistencia si MVC incorpora previsualización; no reservar número ni stock. |
| F5.10 | Pendiente | Verificar el ejemplo CRC 11 187, descuentos 0/25, valores negativos y manipulación de campos calculados. |

## Decisiones de implementación

- Los importes usan `decimal`; cada importe monetario se redondea a dos decimales con `AwayFromZero`, y el subtotal suma las líneas redondeadas. No se imponen enteros ni precisión máxima a las cantidades.
- La fórmula de importes es: subtotal de líneas, descuento redondeado, base gravable, IVA del 13 % y total; cada componente se redondea a dos decimales con `AwayFromZero`. Es una decisión de implementación para el cálculo académico.
- La emisión usa `DateTimeOffset`, hora real del servidor convertida a zona de Costa Rica y presentación de 24 horas `dd/MM/yyyy HH:mm:ss`.
- El número usa prefijo `FAC` más secuencia numérica generada bajo el mismo bloqueo del almacén. Los Id de líneas son Guid asignados por API; al reiniciar se reconstruye solo la semilla.
- No se implementan pago, anulación ni cambios de inventario en F5. El histórico de precios/unidades y la regla de cambio de unidad se resuelven antes de cerrar F6.

## Alcance actual

F5.1 no añade endpoints, almacenes, cálculo, persistencia de facturas, validación de negocio ni cambios a Web. Las pruebas de F5.1 comprueban aislamiento de copias, identidad de detalles y separación entre campos editables y campos asignados/calculados por el servidor.
